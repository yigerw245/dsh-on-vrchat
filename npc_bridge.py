import argparse
import base64
import binascii
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit
from uuid import uuid4
import wave


ROOT = Path(__file__).resolve().parent
LOCAL_MODEL = ROOT.parent / 'live2d_pet' / '.cache' / 'stt' / 'whisper-small'
GREETING = '嗨，我是大肥鱼！有什么想和我聊的？'
MAX_QUESTION = 500
MAX_RECORDING = 2 * 1024 * 1024
WORLD_SAMPLE_RATE = 16000
WORLD_MAX_FRAMES = WORLD_SAMPLE_RATE * 20
WORLD_ACTIONS = {'approach', 'retreat', 'turn', 'stop', 'sit', 'stand'}
WORLD_INPUT = re.compile(r'^\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2} Debug\s+-  \[Dafeiyu input v1\] ([0-9a-f]{32}) ([A-Za-z0-9+/=]{1,2668})$')
WORLD_STATE = re.compile(
    r'^\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2} Debug\s+-  \[Dafeiyu state v1\] '
    r'(standing|seated|moving|held) (\d{1,4}) (front|away) (clear|blocked) '
    r'(idle|arrived|blocked|stopped|sat|stood)$'
)
VRCHAT_LOGS = Path.home() / 'AppData/LocalLow/VRChat/VRChat'


class BridgeError(Exception):
    pass


def parse_answer(answer):
    first_line, _, dialogue = answer.partition('\n')
    action = ''
    if first_line.startswith('ACTION:'):
        requested = first_line.removeprefix('ACTION:').strip()
        if requested in WORLD_ACTIONS:
            action = requested
        answer = dialogue
    reply = ' '.join(answer.split())[:180]
    return reply or '收到。', action


class Dafeiyu:
    def __init__(self, demo, model):
        self.demo = demo
        self.model_path = model
        self.transcriber = None
        self.lock = threading.Lock()
        self.session = None
        self.latest = ('idle', GREETING, b'', b'', '')
        self.latest_world = self.latest
        self.world_state = None

    def transcribe(self, audio):
        if self.transcriber is None:
            try:
                from faster_whisper import WhisperModel
            except ImportError as error:
                raise BridgeError('缺少语音识别组件，请按说明安装 faster-whisper。') from error
            print('正在加载本地语音识别模型。', flush=True)
            self.transcriber = WhisperModel(
                self.model_path, device='cpu', compute_type='int8',
                download_root=str(ROOT / '.dsh-vrchat' / 'whisper'),
            )
        segments, _ = self.transcriber.transcribe(
            io.BytesIO(audio), language='zh', beam_size=1, vad_filter=True,
            condition_on_previous_text=False,
        )
        return ''.join(segment.text for segment in segments).strip()

    def ask(self, question):
        if self.demo:
            return '噗通，我收到啦！这是大肥鱼的本机联调回复。'

        dsh = shutil.which('dsh')
        if not dsh:
            raise BridgeError('找不到 DSH。请先在 DeepSeek Harness 中启用 dsh 命令。')
        env = os.environ.copy()
        env['DSH_HOME'] = str(ROOT / '.dsh-vrchat')
        env.setdefault('VRC_DSH_CREDENTIALS', str(Path.home() / '.dsh' / '.credentials.yaml'))
        env['DSH_TOOLS_MODE'] = 'native'
        command = [dsh]
        dsh_path = Path(dsh)
        if dsh_path.name.lower() == 'dsh.cmd' and len(dsh_path.parents) > 4:
            desktop = dsh_path.parents[4]
            executable = desktop / 'DeepSeek Harness.exe'
            archive = desktop / 'resources' / 'app.asar'
            if executable.is_file() and archive.is_file():
                env['ELECTRON_RUN_AS_NODE'] = '1'
                cli = archive / 'dsh/node_modules/@deepseek-ai/dsh-desktop-host/lib/cli.js'
                command = [str(executable), '--expose-internals', str(cli)]
        command.extend(['--profile', 'headless', '--patch', str(ROOT / 'dafeiyu.patch.yml'), '--json'])
        if self.session:
            command.extend(['--session-id', self.session])
        snapshot = self.world_state
        state = snapshot[1] if snapshot and time.monotonic() - snapshot[0] <= 9 else None
        prompt = json.dumps({'world': state, 'player_message': question}, ensure_ascii=False)
        # Send the question over stdin so speech and chat never become shell arguments.
        try:
            result = subprocess.run(
                [*command, '-'], input=prompt, capture_output=True, encoding='utf-8',
                errors='replace', cwd=ROOT, env=env, timeout=90,
            )
        except subprocess.TimeoutExpired as error:
            self.session = None
            raise BridgeError('DSH 回复超时，请稍后再试。') from error

        answer = ''
        failure_code = ''
        for line in result.stdout.splitlines():
            event = json.loads(line)
            if event.get('type') == 'session':
                self.session = event['sessionId']
            if event.get('type') == 'final':
                answer = event.get('text', '').strip()
            if event.get('phase') == 'turn_end':
                failure_code = (event.get('reason') or {}).get('error', {}).get('code', '')

        if result.returncode or not answer:
            self.session = None
            if failure_code == 'TRANSPORT':
                raise BridgeError('DSH 连不上 DeepSeek。请先确认 DSH 桌面端能正常聊天。')
            raise BridgeError('DSH 没有返回台词。请检查 DeepSeek 登录状态和模型设置。')
        return answer

    def exchange(self, content, is_audio, request_id=None):
        if not self.lock.acquire(blocking=request_id is not None):
            raise BridgeError('大肥鱼还在回复上一句。')
        try:
            question = self.transcribe(content) if is_audio else content.decode('utf-8').strip()
            if not question:
                raise BridgeError('没听清这一句，请再试一次。' if is_audio else '先输入一句话。')
            if len(question) > MAX_QUESTION:
                raise BridgeError(f'每次最多说 {MAX_QUESTION} 个字。')
            reply, action = parse_answer(self.ask(question))
            voice = self.synthesize(reply)
            reply_id = request_id or uuid4().hex
            self.latest = (reply_id, reply, voice, self.world_pcm(voice), action)
            if request_id:
                self.latest_world = self.latest
            return {'question': question, 'reply': reply,
                    'audio': f'/voice.wav?id={reply_id}' if voice else None, 'action': action or None}
        finally:
            self.lock.release()

    def accept_world_message(self, request_id, content):
        try:
            self.exchange(content, is_audio=False, request_id=request_id)
        except BridgeError as error:
            self.latest_world = ('!' + request_id, str(error), b'', b'', '')
        except Exception as error:
            print(f'游戏内对话处理失败 ({type(error).__name__})。', flush=True)
            self.latest_world = ('!' + request_id, '处理失败，请稍后再试。', b'', b'', '')

    @staticmethod
    def world_pcm(wav):
        if wav:
            try:
                with wave.open(io.BytesIO(wav)) as clip:
                    if clip.getnchannels() == 1 and clip.getsampwidth() == 2 and clip.getframerate() == WORLD_SAMPLE_RATE:
                        return clip.readframes(WORLD_MAX_FRAMES)
            except (wave.Error, EOFError):
                pass
        return b''

    @staticmethod
    def synthesize(text):
        powershell = Path(os.environ.get('SystemRoot', r'C:\Windows')) / 'System32/WindowsPowerShell/v1.0/powershell.exe'
        try:
            result = subprocess.run(
                [str(powershell), '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', str(ROOT / 'speak.ps1')],
                input=text.encode('utf-8'), capture_output=True, timeout=15,
            )
            if result.returncode == 0 and result.stdout.startswith(b'RIFF'):
                return result.stdout
        except (OSError, subprocess.TimeoutExpired):
            pass
        print('本机中文语音合成失败，已保留文字回复。', flush=True)
        return b''


def local_test_room(line, current):
    if '  [Behaviour] Entering Room: ' in line:
        return line.rstrip().endswith('  [Behaviour] Entering Room: Local Test')
    if '  [Behaviour] OnLeftRoom' in line:
        return False
    return current


def watch_world_input(log_dir, fish, stop, ready):
    current = None
    stream = None
    initial = True
    in_local_test = False
    try:
        while not stop.is_set():
            try:
                latest = max(log_dir.glob('output_log_*.txt'), default=None)
                if latest and latest != current:
                    if stream:
                        stream.close()
                        stream = None
                    stream = latest.open(encoding='utf-8', errors='replace')
                    current = latest
                    in_local_test = False
                    fish.world_state = None
                    if initial:
                        # Recover the active room without replaying any old submissions.
                        for line in stream:
                            in_local_test = local_test_room(line, in_local_test)
                initial = False
                ready.set()
                while stream and not stop.is_set():
                    position = stream.tell()
                    line = stream.readline()
                    if not line.endswith('\n'):
                        stream.seek(position)
                        break
                    room = local_test_room(line, in_local_test)
                    if not room:
                        fish.world_state = None
                    in_local_test = room
                    state = WORLD_STATE.fullmatch(line.rstrip('\r\n')) if in_local_test else None
                    if state:
                        posture, distance, position, path, result = state.groups()
                        fish.world_state = (time.monotonic(), {
                            'posture': posture, 'distance_m': int(distance) / 100,
                            'position': position, 'nearby_path': path, 'last_motion': result,
                        })
                        continue
                    match = WORLD_INPUT.fullmatch(line.rstrip('\r\n')) if in_local_test else None
                    if match:
                        request_id, encoded = match.groups()
                        try:
                            content = base64.b64decode(encoded, validate=True)
                        except binascii.Error:
                            continue
                        fish.accept_world_message(request_id, content)
            except OSError:
                if stream:
                    stream.close()
                    stream = None
                current = None
                ready.set()
            stop.wait(0.3)
    finally:
        if stream:
            stream.close()


class NpcServer(ThreadingHTTPServer):
    def __init__(self, port, fish):
        super().__init__(('127.0.0.1', port), NpcHandler)
        self.fish = fish
        port = self.server_port
        self.hosts = {f'127.0.0.1:{port}', f'localhost:{port}'}
        self.origins = {f'http://{host}' for host in self.hosts}


class NpcHandler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def send_body(self, data, content_type, status=200):
        self.send_response(status)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(data)))
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.end_headers()
        self.wfile.write(data)

    def send_json(self, data, status=200):
        self.send_body(json.dumps(data, ensure_ascii=False).encode('utf-8'), 'application/json', status)

    def do_GET(self):
        if self.headers.get('Host') not in self.server.hosts:
            self.send_error(403)
            return

        request = urlsplit(self.path)
        reply_id, reply, voice, pcm, action = self.server.fish.latest
        if request.path == '/':
            self.send_body((ROOT / 'talk.html').read_bytes(), 'text/html; charset=utf-8')
        elif request.path == '/npc/reply.txt':
            self.send_body(f'{reply_id}\n{reply}'.encode('utf-8'), 'text/plain; charset=utf-8')
        elif request.path in {'/npc/reply.pcm', '/npc/chat.pcm'}:
            if request.path == '/npc/chat.pcm':
                reply_id, reply, _, pcm, action = self.server.fish.latest_world
            header = f'{reply_id}\n{reply}\n'.encode('utf-8')
            if action:
                header += f'DFY1 {action}\n'.encode('ascii')
            self.send_body(header + pcm, 'application/octet-stream')
        elif request.path == '/voice.wav' and request.query == f'id={reply_id}' and voice:
            self.send_body(voice, 'audio/wav')
        else:
            self.send_error(404)

    def do_POST(self):
        if self.headers.get('Host') not in self.server.hosts or self.headers.get('Origin') not in self.server.origins:
            self.send_error(403)
            return
        if self.path != '/talk':
            self.send_error(404)
            return

        content_type = self.headers.get_content_type()
        is_audio = content_type in {'audio/webm', 'audio/ogg', 'audio/mp4', 'audio/wav'}
        if not is_audio and content_type != 'text/plain':
            self.send_error(415)
            return
        try:
            size = int(self.headers.get('Content-Length', 0))
            if not 0 < size <= (MAX_RECORDING if is_audio else MAX_QUESTION * 4):
                self.send_error(413)
                return
            self.connection.settimeout(10)
            body = self.rfile.read(size)
            if len(body) != size:
                self.send_error(400)
                return
            result = self.server.fish.exchange(body, is_audio)
        except BridgeError as error:
            self.send_json({'error': str(error)}, 422)
        except Exception as error:
            print(f'处理失败 ({type(error).__name__}): {error}', flush=True)
            self.send_json({'error': '处理失败，请查看启动窗口后再试。'}, 500)
        else:
            self.send_json(result)


def main():
    parser = argparse.ArgumentParser(description='大肥鱼 VRChat NPC 本地对话服务')
    parser.add_argument('--port', type=int, default=17821)
    parser.add_argument('--model', default=str(LOCAL_MODEL) if (LOCAL_MODEL / 'model.bin').is_file() else 'small')
    parser.add_argument('--demo', action='store_true', help='用固定回复测试语音和世界接线')
    args = parser.parse_args()
    fish = Dafeiyu(args.demo, args.model)
    with NpcServer(args.port, fish) as server:
        stop = threading.Event()
        ready = threading.Event()
        threading.Thread(target=watch_world_input, args=(VRCHAT_LOGS, fish, stop, ready), daemon=True).start()
        ready.wait(timeout=3)
        print(f'对话页面 http://127.0.0.1:{server.server_port}/', flush=True)
        print(f'世界字幕与配音 http://127.0.0.1:{server.server_port}/npc/reply.pcm', flush=True)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass
        finally:
            stop.set()


if __name__ == '__main__':
    main()
