using System;
using System.Text;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DafeiyuNpc : UdonSharpBehaviour
{
    public VRCUrl replyUrl;
    public VRCUrl chatUrl;
    public TMP_InputField chatInput;
    public TMP_Text chatReply;
    public Button chatSend;
    public TextMeshPro subtitles;
    public Transform subtitleAnchor;
    public Transform whale;
    public Transform tail;
    public AudioSource voice;

    private const int SampleRate = 16000;
    private const int MaxSamples = SampleRate * 20;
    private const int SamplesPerFrame = 4096;
    private const float ChatTimeout = 150f;
    private const float ChatFollowDistance = 0.44f;
    private const float ChatTurnThreshold = 50f;
    private const int BonesPerRig = 18;
    private const float WalkDuration = 0.88f;
    private const float StumbleDuration = 0.96f;
    private const float TurnDuration = 1.35f;
    private const float ApproachGap = 0.92f;
    private const float ApproachTolerance = 0.16f;
    private const float SitDrop = 0.315f;
    private const float SitFootReach = 0.31f;
    private const float LandingDuration = 0.44f;
    private const float WalkCycleDistance = 0.49f;
    private const float ApproachSpeed = 0.55f;
    private const int LeftWristSwing = 6;
    private const int LeftKneeSwing = 8;
    private const int LeftAnkleSwing = 10;
    private const int NeckSwing = 12;
    private const int LeftShoulderSwing = 13;
    private const int LeftShoulderBone = 15;
    private const int HipsBone = 17;

    private Vector3 _restPosition;
    private Rigidbody _body;
    private BoxCollider _torsoCollider;
    private Vector3 _standingTorsoCenter;
    private Vector3 _standingTorsoSize;
    private Vector3 _standingHeadCenter;
    private Vector3 _standingSubtitle;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private int _standingLayer;
    private int _pickupLayer;
    private bool _held;
    private SphereCollider _headCollider;
    private VRC_Pickup _pickup;
    private bool _touchingHead;
    private float _nextHeadPat;
    private float _headPatStarted = -10f;
    private float _nextBodyBump;
    private float _bumpStarted = -10f;
    private bool _stumbling;
    private bool _stepping;
    private bool _turning;
    private bool _approaching;
    private bool _seated;
    private float _sitBlend;
    private float _approachUntil;
    private float _walkPhase;
    private float _walkSpeed;
    private float _walkBlend;
    private string _lastMotionResult = "idle";
    private string _lastWorldState;
    private float _nextWorldState;
    private float _worldStateHeartbeat;
    private float _stepStarted;
    private float _stepDuration;
    private Vector3 _stepStart;
    private Vector3 _stepDirection;
    private float _stepDistance;
    private Quaternion _turnStartFacing;
    private bool _leftFootFirst;
    private bool _legsWereStepping;
    private Quaternion _carryFacing;
    private Vector3 _lastCarryPosition;
    private Vector3 _lastCarryVelocity;
    private Vector3 _carryAcceleration;
    private Vector3 _carryTilt;
    private Vector3 _carryTiltVelocity;
    private Vector3[] _limbSwing = new Vector3[15];
    private Vector3[] _limbVelocity = new Vector3[15];
    private Vector3[] _armpitGrip = new Vector3[2];
    private int[] _armpitSide = { -1, -1 };
    private float _carryBlend;
    private float _carryRoll;
    private bool _waitingForLanding;
    private float _fallSpeed;
    private float _landingStarted = -10f;
    private float _landingDepth;
    private float _landingCompression;
    private Quaternion _visualFacing;
    private float _reactionCaptionUntil;
    private string _lastSubtitle;
    private Quaternion _tailRestRotation;
    private float _nextPoll;
    private float _replyAnimationUntil;
    private bool _downloading;
    private bool _downloadingChat;
    private string _lastReplyId;
    private string _pendingChatId;
    private float _chatDeadline;
    private Transform _chatPanel;
    private bool _chatOpen;
    private Vector3 _chatHeadAnchor;
    private Quaternion _chatHeading;
    private Vector3 _chatPosition;
    private Quaternion _chatRotation;
    private float _nextChatToggle;
    private short[] _pcm;
    private float[] _samples;
    private int _sampleOffset;
    private AudioClip _generatedClip;
    private const int SpeechWindow = SampleRate / 50;
    private float[] _speechEnvelope;
    private float[] _nextSpeechEnvelope;
    private float _mouthOpening;
    private float _nextBlink;
    private Transform[] _poseBones;
    private Quaternion[] _boneRest;
    private Quaternion[] _boneAxes;
    private Vector3[] _footRest;
    private Transform[] _skirtPanels;
    private Quaternion[] _skirtRest;
    private Vector3[] _skirtLiftAxis;
    private Vector3[] _skirtRadial;
    private bool _skirtPosed;
    // Quaternius, Universal Animation Library (CC0), Walk_Formal_Loop.
    // Ankle travel, lift and pitch, scaled to Kipfel's legs. The right foot is half a cycle apart.
    private Vector3[] _walkAnkle =
    {
        new Vector3(0.114f, 0.000f, -9f), new Vector3(0.085f, 0.000f, -4f),
        new Vector3(0.055f, 0.000f, 0f), new Vector3(0.026f, 0.000f, 0f),
        new Vector3(-0.002f, 0.002f, 1f), new Vector3(-0.028f, 0.007f, 3f),
        new Vector3(-0.052f, 0.012f, 6f), new Vector3(-0.075f, 0.016f, 8f),
        new Vector3(-0.102f, 0.018f, 9f), new Vector3(-0.109f, 0.021f, 11f),
        new Vector3(-0.117f, 0.041f, 18f), new Vector3(-0.089f, 0.065f, 26f),
        new Vector3(-0.034f, 0.075f, 28f), new Vector3(0.024f, 0.065f, 20f),
        new Vector3(0.073f, 0.042f, 5f), new Vector3(0.102f, 0.014f, -5f)
    };
    private int[] _grippedBy = { -1, -1 };
    private Vector3[] _wristTarget = new Vector3[2];
    private float[] _gripWeight = new float[2];
    private int[] _shoulderHeldBy = { -1, -1 };
    private Vector3[] _shoulderGripOffset = new Vector3[2];
    private Vector3 _shoulderChestSwing;
    private Vector3 _shoulderChestVelocity;
    private Vector3 _shoulderHipSwing;
    private Vector3 _shoulderHipVelocity;
    private bool[] _touchingBody = new bool[2];
    private Vector3[] _carryOffset = new Vector3[2];
    private int _headHeldBy = -1;
    private Vector3 _headGripOffset;
    private Quaternion _headGripHandRotation;
    private Vector3 _headGripAngles;
    private float _headGripWeight;
    private bool _manualCarry;
    private Vector3 _carryTarget;
    private Vector3 _releaseVelocity;
    private Vector3 _touchPush;
    private Vector3 _touchVelocity;
    private float _nextTouchFeedback;
    private SkinnedMeshRenderer _face;
    private int _blinkShape = -1;
    private int _eyeHappyShape = -1;
    private int _mouthShape = -1;
    private int _smileShape = -1;
    private float _happyEyes;

    private void Start()
    {
        InteractionText = "和大肥鱼聊天";
        _body = GetComponent<Rigidbody>();
        _torsoCollider = GetComponent<BoxCollider>();
        _pickup = GetComponent<VRC_Pickup>();
        _headCollider = GetComponent<SphereCollider>();
        if (_torsoCollider != null)
        {
            _standingTorsoCenter = _torsoCollider.center;
            _standingTorsoSize = _torsoCollider.size;
        }
        if (_headCollider != null) _standingHeadCenter = _headCollider.center;
        _standingSubtitle = subtitleAnchor.localPosition;
        _visualFacing = whale.rotation;
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
        _standingLayer = gameObject.layer;
        _pickupLayer = LayerMask.NameToLayer("Pickup");
        _lastSubtitle = subtitles.text;
        if (_body != null) DisableInteractive = true;
        _restPosition = whale.localPosition;
        if (tail != null) _tailRestRotation = tail.localRotation;
        InitializeKipfelPose();
        if (_poseBones != null && _headCollider != null && _pickup != null) _pickup.pickupable = false;
        if (chatInput != null)
        {
            // The input is a direct child of the world-space chat canvas.
            _chatPanel = chatInput.transform.parent;
            _chatPanel.localScale *= 0.66f;
            _chatPanel.gameObject.SetActive(false);
        }
    }

    public override void Interact()
    {
        _replyAnimationUntil = Time.time + 2f;
        if (string.IsNullOrEmpty(_lastReplyId) || _lastReplyId == "idle")
        {
            subtitles.text = chatInput != null ? "嗨！打开对话框给我留言吧。" : "嗨！在本机的对话页面说一句，我就在这儿回你。";
        }
        PollReply();
    }

    public override void OnPickup()
    {
        _approaching = false;
        _walkBlend = 0f;
        _seated = false;
        _lastMotionResult = "idle";
        _grippedBy[0] = -1;
        _grippedBy[1] = -1;
        _shoulderHeldBy[0] = -1;
        _shoulderHeldBy[1] = -1;
        _stumbling = false;
        _stepping = false;
        _turning = false;
        _held = true;
        _carryFacing = _visualFacing;
        _waitingForLanding = false;
        _landingStarted = -10f;
        _landingCompression = 0f;
        _lastCarryPosition = transform.position;
        _lastCarryVelocity = Vector3.zero;
        _carryAcceleration = Vector3.zero;
        if (_pickupLayer >= 0) gameObject.layer = _pickupLayer;
        ShowReaction("嘿嘿，被你抱起来啦！");
        Debug.Log("[Dafeiyu] Picked up.");
    }

    public override void OnDrop()
    {
        _held = false;
        gameObject.layer = _standingLayer;
        Vector3 forward = _poseBones == null ? whale.forward : _carryFacing * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.01f) _visualFacing = Quaternion.LookRotation(forward);
        if (_body != null && _torsoCollider != null && _poseBones != null)
        {
            _fallSpeed = Mathf.Max(0f, -_body.velocity.y);
            _waitingForLanding = !HasFloorAt(Vector3.zero, 0.025f) || Mathf.Abs(_body.velocity.y) >= 0.35f;
        }
        Debug.Log("[Dafeiyu] Put down.");
    }

    public override void OnPlayerTriggerStay(VRCPlayerApi player)
    {
        // VRChat sends player trigger events even when its player capsule walks into a stationary object.
        if (!player.isLocal || _headCollider == null || _held || _waitingForLanding || (_stepping && !_approaching) || _sitBlend > 0f
            || UpperBodyHeld() || Time.time < _nextBodyBump) return;
        Vector3 away = transform.position - player.GetPosition();
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f) return;
        away.Normalize();
        float approachSpeed = Vector3.Dot(player.GetVelocity(), away);
        if (approachSpeed < 0.30f) return;

        _approaching = false;
        _walkSpeed = 0f;
        _walkBlend = 0f;
        float distance = Mathf.Clamp(0.36f + approachSpeed * 0.12f, 0.40f, 0.62f);
        if (!HasWalkingSpace(away, distance))
            distance = HasWalkingSpace(away, 0.25f) ? 0.25f : 0f;

        // Preserve the facing at impact. The push direction only moves the body.
        BeginSteps(away, distance, false);
        _stumbling = true;
        _stepDuration = StumbleDuration;
        _bumpStarted = _stepStarted;
        _nextBodyBump = _stepStarted + StumbleDuration + 0.20f;
        ShowReaction("哎呀，碰到我啦！");
        Debug.Log("[Dafeiyu] Bumped by player. Stumbling backward.");
    }

    private void BeginSteps(Vector3 direction, float distance, bool turn)
    {
        _stepping = true;
        _turning = turn;
        _stumbling = false;
        _stepStarted = Time.time;
        _stepDuration = turn ? TurnDuration : WalkDuration;
        _stepStart = _body.position;
        _stepDirection = direction;
        _stepDistance = distance;
        _turnStartFacing = _visualFacing;
        float side = Vector3.Dot(direction, whale.right);
        if (Mathf.Abs(side) > 0.25f) _leftFootFirst = side < 0f;
        else _leftFootFirst = !_leftFootFirst;
    }

    private void StopSteps()
    {
        if (!_stepping) return;
        _stepping = false;
        _turning = false;
        _stumbling = false;
        _body.velocity = Vector3.up * _body.velocity.y;
    }

    private bool HasFloor(Vector3 offset)
    {
        return HasFloorAt(offset, 0.14f);
    }

    private bool HasFloorAt(Vector3 offset, float groundReach)
    {
        Vector3 probe = _body.position + offset;
        probe.y = _torsoCollider.bounds.min.y + 0.10f;
        return Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 0.10f + groundReach, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider != _torsoCollider && hit.normal.y >= 0.6f;
    }

    private void RunMovement(string action)
    {
        if (action != "approach" && action != "retreat" && action != "turn" && action != "stop"
            && action != "sit" && action != "stand") return;
        if (_body == null || _torsoCollider == null) return;
        if (action == "stop")
        {
            _approaching = false;
            StopSteps();
            _lastMotionResult = "stopped";
            Debug.Log("[Dafeiyu] Movement command: stop");
            return;
        }
        if (_held || UpperBodyHeld())
        {
            ShowReaction("等你松开我再动。");
            return;
        }
        if (_waitingForLanding || Time.time - _landingStarted < LandingDuration || !HasFloor(Vector3.zero))
        {
            ShowReaction("等我站稳再走。");
            return;
        }
        _approaching = false;
        StopSteps();
        _lastMotionResult = "idle";
        if (action == "sit" || action == "stand")
        {
            if (_poseBones == null || _poseBones[8] == null || _poseBones[11] == null) return;
            if (action == "sit" && (!HasFloor(whale.forward * (SitFootReach + 0.08f))
                || !HasWalkingSpace(whale.forward, 0.25f)))
            {
                ShowReaction("这里有点挤，我坐不下。");
                _lastMotionResult = "blocked";
                return;
            }
            _seated = action == "sit";
            Debug.Log("[Dafeiyu] Movement command: " + action);
            return;
        }
        _seated = false;
        if (action == "approach")
        {
            _approaching = true;
            _walkSpeed = 0f;
            _approachUntil = Time.time + 35f;
            Debug.Log("[Dafeiyu] Movement command: approach");
            return;
        }
        if (_sitBlend > 0f)
        {
            ShowReaction("等我先站稳。");
            return;
        }
        if (action == "turn")
        {
            BeginSteps(Vector3.zero, 0f, true);
        }
        else
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player)) return;
            Vector3 towards = player.GetPosition() - _body.position;
            towards.y = 0f;
            Vector3 facing = towards.sqrMagnitude > 0.001f ? towards.normalized : _visualFacing * Vector3.forward;
            Vector3 direction = -facing;
            const float distance = 0.48f;
            if (!HasWalkingSpace(direction, distance))
            {
                ShowReaction("再走就要掉下去啦。");
                return;
            }
            _visualFacing = Quaternion.LookRotation(facing);
            BeginSteps(direction, distance, false);
        }
        Debug.Log("[Dafeiyu] Movement command: " + action);
    }

    private Vector3 PlayerFront(VRCPlayerApi player)
    {
        Vector3 forward = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.16f)
        {
            forward = player.GetRotation() * Vector3.forward;
            forward.y = 0f;
        }
        return player.GetPosition() + forward.normalized * ApproachGap;
    }

    private bool HasWalkingSpace(Vector3 direction, float distance)
    {
        return HasFloor(direction * (distance * 0.5f)) && HasFloor(direction * (distance + 0.18f))
            && (!_body.SweepTest(direction, out RaycastHit hit, distance + 0.04f, QueryTriggerInteraction.Ignore)
                || hit.normal.y >= 0.6f);
    }

    private void ContinueApproach()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || UpperBodyHeld() || Time.time >= _approachUntil)
        {
            FinishApproach("stopped", "我先在这里停一下。");
            return;
        }
        if (_sitBlend > 0f) return;
        Vector3 front = PlayerFront(player);
        Vector3 remaining = front - _body.position;
        if (Mathf.Abs(remaining.y) > 0.55f || !HasFloor(Vector3.zero))
        {
            FinishApproach("blocked", "你那边的地面我过不去。");
            return;
        }
        remaining.y = 0f;
        if (remaining.sqrMagnitude <= ApproachTolerance * ApproachTolerance)
        {
            FinishApproach("arrived", "我到你面前啦。");
            return;
        }

        Vector3 direct = remaining.normalized;
        float stride = Mathf.Min(0.52f, remaining.magnitude);
        Vector3 fromPlayer = _body.position - player.GetPosition();
        fromPlayer.y = 0f;
        for (int option = 0; option < 7; option++)
        {
            float angle = ((option + 1) / 2) * 32f * (option % 2 == 1 ? 1f : -1f);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * direct;
            float closest = Mathf.Clamp(-Vector3.Dot(fromPlayer, direction), 0f, stride);
            if ((fromPlayer + direction * closest).sqrMagnitude < 0.49f
                && Vector3.Dot(fromPlayer, direction) < 0.05f) continue;
            if (!HasWalkingSpace(direction, stride)) continue;
            BeginSteps(direction, stride, false);
            return;
        }
        FinishApproach("blocked", "前面过不去，我先停在这里。");
    }

    private void FinishApproach(string result, string caption)
    {
        _approaching = false;
        _walkSpeed = 0f;
        _lastMotionResult = result;
        ShowReaction(caption);
        Debug.Log("[Dafeiyu] Approach result: " + result);
    }

    private void FixedUpdate()
    {
        if (_body == null) return;
        if (_manualCarry)
        {
            Vector3 before = _body.position;
            Vector3 next = Vector3.MoveTowards(before, _carryTarget, Time.fixedDeltaTime * 3.5f);
            Vector3 offset = next - before;
            if (offset.sqrMagnitude > 0.000001f && _body.SweepTest(offset.normalized, out RaycastHit hit,
                offset.magnitude + 0.01f, QueryTriggerInteraction.Ignore))
            {
                next = before + offset.normalized * Mathf.Max(0f, hit.distance - 0.01f);
            }
            _body.MovePosition(next);
            _releaseVelocity = Vector3.Lerp(_releaseVelocity, (next - before) / Time.fixedDeltaTime, 0.35f);
            return;
        }
        if (_held) return;
        if (_body.position.y < _spawnPosition.y - 4f)
        {
            _waitingForLanding = false;
            _approaching = false;
            _seated = false;
            StopSteps();
            _body.position = _spawnPosition;
            _body.rotation = _spawnRotation;
            _body.velocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }
        if (_waitingForLanding)
        {
            float downSpeed = -_body.velocity.y;
            _fallSpeed = Mathf.Max(_fallSpeed, downSpeed);
            if (Mathf.Abs(downSpeed) > 0.20f || !HasFloorAt(Vector3.zero, 0.025f)) return;
            _waitingForLanding = false;
            _landingDepth = Mathf.Clamp(0.033f + _fallSpeed * 0.018f, 0.033f, 0.083f);
            _landingStarted = Time.time;
            _nextBodyBump = Mathf.Max(_nextBodyBump, Time.time + LandingDuration);
            Debug.Log("[Dafeiyu] Landed with knee bend. Impact speed: " + _fallSpeed);
        }
        if (!_stepping)
        {
            if (_approaching) ContinueApproach();
            if (!_stepping) return;
        }

        float elapsed = Time.time - _stepStarted;
        Vector3 verticalVelocity = Vector3.up * _body.velocity.y;
        float traveled = Vector3.Dot(_body.position - _stepStart, _stepDirection);
        if (_approaching)
        {
            WalkToPlayer(traveled, elapsed, verticalVelocity);
            return;
        }
        if (elapsed >= _stepDuration || _body.position.y < _stepStart.y - 0.18f)
        {
            bool stumble = _stumbling;
            StopSteps();
            Debug.Log("[Dafeiyu] " + (stumble ? "Recovered from stumble. Distance: " : "Finished steps. Distance: ") + traveled);
            return;
        }
        float nextPhase = Mathf.Clamp01((elapsed + Time.fixedDeltaTime) / _stepDuration);
        float distance = (_stumbling ? StumbleTravel(nextPhase) : StepTravel(nextPhase)) * _stepDistance;
        float speed = Mathf.Clamp((distance - traveled) / Time.fixedDeltaTime, 0f, _stumbling ? 1.7f : 1.1f);
        _body.velocity = verticalVelocity + _stepDirection * speed;
    }

    private void WalkToPlayer(float traveled, float elapsed, Vector3 verticalVelocity)
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || UpperBodyHeld() || Time.time >= _approachUntil)
        {
            StopSteps();
            FinishApproach("stopped", "我先在这里停一下。");
            return;
        }
        Vector3 fromPlayer = _body.position - player.GetPosition();
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude < 0.49f && Vector3.Dot(fromPlayer, _stepDirection) < 0f)
        {
            StopSteps();
            _walkSpeed = 0f;
            return;
        }
        if (_body.position.y < _stepStart.y - 0.18f || (elapsed > 2.1f && _stepDistance - traveled >= 0.035f))
        {
            StopSteps();
            FinishApproach("blocked", "路被挡住啦，我先停在这里。");
            return;
        }
        if (_stepDistance - traveled < 0.035f)
        {
            StopSteps();
            ContinueApproach();
            if (!_stepping) return;
        }
        Vector3 toGoal = PlayerFront(player) - _body.position;
        toGoal.y = 0f;
        float targetSpeed = Mathf.Clamp((toGoal.magnitude - 0.07f) * 2.4f, 0.18f, ApproachSpeed);
        _walkSpeed = Mathf.MoveTowards(_walkSpeed, targetSpeed, Time.fixedDeltaTime * 2.2f);
        _body.velocity = verticalVelocity + _stepDirection * _walkSpeed;
    }

    private float StepTravel(float phase)
    {
        // The body travels a little less on the opening and closing steps.
        float step = phase * 3f;
        if (step < 1f) return Mathf.SmoothStep(0f, 0.24f, step);
        if (step < 2f) return Mathf.SmoothStep(0.24f, 0.76f, step - 1f);
        return Mathf.SmoothStep(0.76f, 1f, step - 2f);
    }

    private float StumbleTravel(float phase)
    {
        if (phase < 0.30f) return Mathf.SmoothStep(0f, 0.43f, phase / 0.30f);
        if (phase < 0.63f) return Mathf.SmoothStep(0.43f, 0.82f, (phase - 0.30f) / 0.33f);
        return Mathf.SmoothStep(0.82f, 1f, (phase - 0.63f) / 0.37f);
    }

    private void Update()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) return;
        if (!player.IsUserInVR() && Input.GetKeyDown(KeyCode.F9)) ToggleChat(player);
        Vector3 towardsPlayer = player.GetPosition() - transform.position;
        if (_reactionCaptionUntil > 0f && Time.time >= _reactionCaptionUntil)
        {
            subtitles.text = _lastSubtitle;
            _reactionCaptionUntil = 0f;
        }
        if (!string.IsNullOrEmpty(_pendingChatId) && Time.time >= _chatDeadline)
        {
            FinishChat("大肥鱼还没收到回复，请检查本机对话服务后再试。", false);
        }
        bool nearby = towardsPlayer.sqrMagnitude <= 36f;
        if (Time.time >= _nextWorldState) ReportWorldState(player, false);
        // A request from the local webpage can ask the NPC to cross the entire world.
        PollReply();
        if (_pcm != null) DecodeVoiceFrame();
        float landingAge = Time.time - _landingStarted;
        float compression = landingAge < 0.11f
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(landingAge / 0.11f))
            : Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((landingAge - 0.11f) / (LandingDuration - 0.11f)));
        _landingCompression = compression * _landingDepth;
        if (!nearby && !_held && !_waitingForLanding && _landingCompression <= 0f && !_stepping
            && !_approaching && !_legsWereStepping && !_seated && _sitBlend <= 0f) return;
        UpdateSeating();
        float walking = _approaching && _stepping ? _walkSpeed / ApproachSpeed : 0f;
        _walkBlend = Mathf.MoveTowards(_walkBlend, walking, Time.deltaTime * 4.5f);
        if (walking > 0f)
        {
            Vector3 velocity = _body.velocity;
            velocity.y = 0f;
            _walkPhase = Mathf.Repeat(_walkPhase + velocity.magnitude * Time.deltaTime / WalkCycleDistance, 1f);
        }
        if (tail != null)
        {
            float amplitude = Time.time < _replyAnimationUntil ? 0.085f : 0.035f;
            whale.localPosition = _restPosition + Vector3.up * (Mathf.Sin(Time.time * 3f) * amplitude);
            tail.localRotation = _tailRestRotation * Quaternion.Euler(Mathf.Sin(Time.time * 5f) * 16f, 0f, 0f);
        }
        towardsPlayer.y = 0f;
        if (towardsPlayer.sqrMagnitude > 0.01f)
        {
            if (!_held && !UpperBodyHeld())
            {
                Vector3 look = _approaching && _stepping ? _stepDirection : towardsPlayer;
                if (!_stepping || _approaching)
                    _visualFacing = Quaternion.Slerp(_visualFacing, Quaternion.LookRotation(look), Time.deltaTime * 6f);
            }
            // TextMeshPro text is readable from its local -Z side.
            subtitleAnchor.rotation = Quaternion.LookRotation(-towardsPlayer);
        }
        if (!_held)
        {
            if (_turning)
            {
                float turnPhase = Mathf.Clamp01((Time.time - _stepStarted) / _stepDuration);
                _visualFacing = Quaternion.AngleAxis(360f * Mathf.SmoothStep(0f, 1f, turnPhase), Vector3.up) * _turnStartFacing;
            }
            float nod = _poseBones == null ? ReactionPulse(Time.time - _headPatStarted, 0.65f) * 8f : 0f;
            whale.rotation = _visualFacing * Quaternion.Euler(nod, 0f, 0f);
        }
        if (_poseBones != null)
        {
            UpdateCarryPhysics(player);
            AnimateKipfel();
        }
    }

    private void UpdateSeating()
    {
        float blend = Mathf.MoveTowards(_sitBlend, _seated ? 1f : 0f, Time.deltaTime * 2.2f);
        if (_sitBlend == blend) return;
        _sitBlend = blend;
        float drop = SitDrop * Mathf.SmoothStep(0f, 1f, blend);
        if (_headCollider != null) _headCollider.center = _standingHeadCenter - Vector3.up * drop;
        _torsoCollider.size = _standingTorsoSize - Vector3.up * drop;
        _torsoCollider.center = _standingTorsoCenter - Vector3.up * (drop * 0.5f);
        subtitleAnchor.localPosition = _standingSubtitle - Vector3.up * drop;
        if (blend == 1f || blend == 0f)
        {
            _lastMotionResult = _seated ? "sat" : "stood";
            Debug.Log("[Dafeiyu] Posture: " + _lastMotionResult);
        }
    }

    private void ReportWorldState(VRCPlayerApi player, bool force)
    {
        _nextWorldState = Time.time + 1f;
        if (_body == null || _torsoCollider == null) return;
        Vector3 toPlayer = player.GetPosition() - _body.position;
        toPlayer.y = 0f;
        int distance = Mathf.Min(9999, Mathf.RoundToInt(toPlayer.magnitude * 100f));
        Vector3 ahead = PlayerFront(player) - _body.position;
        ahead.y = 0f;
        bool atFront = ahead.sqrMagnitude <= ApproachTolerance * ApproachTolerance;
        bool clearAhead = !_held && (atFront || HasWalkingSpace(ahead.normalized, Mathf.Min(0.52f, ahead.magnitude)));
        string posture = _held ? "held" : (_approaching || _stepping ? "moving" : (_seated ? "seated" : "standing"));
        string state = posture + " " + distance + " " + (atFront ? "front" : "away") + " "
            + (clearAhead ? "clear" : "blocked") + " " + _lastMotionResult;
        if (!force && state == _lastWorldState && Time.time < _worldStateHeartbeat) return;
        _worldStateHeartbeat = Time.time + 4f;
        _lastWorldState = state;
        Debug.Log("[Dafeiyu state v1] " + state);
    }

    private void UpdateCarryPhysics(VRCPlayerApi player)
    {
        float delta = Mathf.Clamp(Time.deltaTime, 0.001f, 0.035f);
        float settle = 1f - Mathf.Exp(-14f * delta);
        _carryBlend = Mathf.MoveTowards(_carryBlend, _held ? 1f : 0f, delta * 5f);
        if (_held)
        {
            Vector3 toPlayer = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position - whale.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0225f)
                _carryFacing = Quaternion.RotateTowards(_carryFacing, Quaternion.LookRotation(toPlayer), 960f * delta);
        }
        Quaternion baseRotation = _held ? _carryFacing : whale.rotation;
        Vector3 drive = Vector3.zero;
        if (_held)
        {
            Vector3 velocity = Vector3.ClampMagnitude((transform.position - _lastCarryPosition) / delta, 4f);
            _lastCarryPosition = transform.position;
            Vector3 filtered = Vector3.Lerp(_lastCarryVelocity, velocity, settle);
            drive = Quaternion.Inverse(baseRotation) * (filtered - _lastCarryVelocity) / delta;
            _lastCarryVelocity = filtered;
        }
        _carryAcceleration = Vector3.Lerp(_carryAcceleration, Vector3.ClampMagnitude(drive, 22f), settle);
        Vector3 acceleration = _carryAcceleration;
        float roll = _manualCarry ? _carryRoll : 0f;
        Vector3 bodyTarget = new Vector3(-acceleration.z * 1.40f, 0f, acceleration.x * 1.45f + roll) * _carryBlend;
        _carryTilt = DampedAngle(_carryTilt, bodyTarget, ref _carryTiltVelocity, 55f, 8.5f, delta, 34f);

        Vector3 chestTarget = Vector3.zero;
        for (int side = 0; side < 2; side++)
        {
            float outward = side == 0 ? -1f : 1f;
            Vector3 swing = new Vector3(-acceleration.z, 0f, acceleration.x) * _carryBlend;
            Vector3 supportedArm = swing * 2.8f;
            supportedArm.z += outward * (10f + acceleration.y * 0.7f) * _carryBlend;
            SwingJoint(side, supportedArm, 68f, 10f, delta, 29f);
            SwingJoint(2 + side, swing * 3.7f - _limbSwing[side] * 0.32f, 47f, 7.5f, delta, 34f);
            SwingJoint(4 + side, swing * 2.6f, 46f, 6.5f, delta, 34f);

            Vector3 wrist = swing * 0.8f - _limbSwing[2 + side] * 0.28f;
            SwingJoint(LeftWristSwing + side, wrist * (1f - _gripWeight[side]), 45f, 7f, delta, 13f);

            Vector3 thigh = _limbSwing[4 + side];
            float knee = Mathf.Clamp(thigh.x * 0.52f + acceleration.z * 0.65f * _carryBlend, -25f, 0f);
            SwingJoint(LeftKneeSwing + side, new Vector3(knee, 0f, -thigh.z * 0.18f), 52f, 8.5f, delta, 26f);
            Vector3 lowerLeg = _limbSwing[LeftKneeSwing + side];
            SwingJoint(LeftAnkleSwing + side, -0.28f * (thigh + lowerLeg) + swing * 0.28f, 44f, 7.5f, delta, 11f);

            int shoulderOwner = _shoulderHeldBy[side];
            Vector3 pull = shoulderOwner < 0 ? Vector3.zero : Vector3.ClampMagnitude(
                whale.InverseTransformPoint(PalmPosition(player, shoulderOwner)) - _shoulderGripOffset[side], 0.19f);
            Vector3 shoulderTarget = _limbSwing[side] * 0.18f
                + new Vector3(-pull.z * 35f, -outward * pull.z * 110f, outward * pull.y * 115f - pull.x * 32f);
            SwingJoint(LeftShoulderSwing + side, Vector3.ClampMagnitude(shoulderTarget, 17f), 124f, 13f, delta, 19f);
            chestTarget += new Vector3(pull.z * 48f, -outward * pull.z * 46f, -pull.x * 64f + outward * pull.y * 30f);
        }
        _shoulderChestSwing = DampedAngle(_shoulderChestSwing, Vector3.ClampMagnitude(chestTarget, 11f),
            ref _shoulderChestVelocity, 85f, 12f, delta, 13f);
        Vector3 hipTarget = _shoulderChestSwing * (0.58f * (1f - _carryBlend));
        _shoulderHipSwing = DampedAngle(_shoulderHipSwing, hipTarget,
            ref _shoulderHipVelocity, 48f, 8f, delta, 8f);
        Vector3 neckTarget = (-0.42f * _carryTilt + new Vector3(acceleration.z * 0.4f, 0f, -acceleration.x * 0.4f))
            * _carryBlend * (1f - _headGripWeight);
        SwingJoint(NeckSwing, neckTarget, 56f, 8f, delta, 11f);

        whale.rotation = baseRotation * Quaternion.Euler(_carryTilt);
        if (tail == null && (!_stepping || _approaching))
        {
            Vector3 pivot = _manualCarry ? (_armpitGrip[0] + _armpitGrip[1]) * 0.5f : Vector3.zero;
            Vector3 scaledPivot = Vector3.Scale(pivot, whale.lossyScale);
            float seat = Mathf.SmoothStep(0f, 1f, _sitBlend);
            Vector3 hipShift = new Vector3(-_shoulderHipSwing.z * 0.006f,
                -_shoulderHipSwing.magnitude * 0.003f - SitDrop * seat - _landingCompression,
                _shoulderHipSwing.x * 0.006f);
            float sway = Mathf.Sin((_walkPhase - 0.17f) * (2f * Mathf.PI));
            float dip = Mathf.Cos(_walkPhase * (4f * Mathf.PI));
            hipShift += new Vector3(sway * 0.010f, -0.017f - dip * 0.007f, 0f) * _walkBlend;
            whale.position = transform.TransformPoint(_restPosition) + baseRotation * (scaledPivot + hipShift)
                - whale.rotation * scaledPivot;
        }
    }

    private Vector3 DampedAngle(Vector3 angle, Vector3 target, ref Vector3 velocity, float stiffness, float damping,
        float delta, float limit)
    {
        velocity += ((target - angle) * stiffness - velocity * damping) * delta;
        Vector3 next = angle + velocity * delta;
        if (next.sqrMagnitude <= limit * limit) return next;
        Vector3 outward = next.normalized;
        velocity -= outward * Mathf.Max(0f, Vector3.Dot(velocity, outward));
        return outward * limit;
    }

    private void SwingJoint(int joint, Vector3 target, float stiffness, float damping, float delta, float limit)
    {
        _limbSwing[joint] = DampedAngle(_limbSwing[joint], target, ref _limbVelocity[joint], stiffness, damping, delta, limit);
    }

    private void InitializeKipfelPose()
    {
        if (whale.childCount == 0) return;
        Transform model = whale.GetChild(0);
        Transform mainChest = model.Find("armature/Hips/Spine/Chest");
        if (mainChest == null) return;
        InitializeSkirt(model);

        string[] rigPaths = { "armature", "女仆装/armature.1", "胖次/armature.1" };
        string[] bonePaths = { "Spine/Chest", "Spine/Chest/Neck/Head",
            "Spine/Chest/Shoulder.L/UpperArm.L", "Spine/Chest/Shoulder.R/UpperArm.R",
            "Spine/Chest/Shoulder.L/UpperArm.L/LowerArm.L", "Spine/Chest/Shoulder.R/UpperArm.R/LowerArm.R",
            "UpperLeg.L", "UpperLeg.L/LowerLeg.L", "UpperLeg.L/LowerLeg.L/foot.L",
            "UpperLeg.R", "UpperLeg.R/LowerLeg.R", "UpperLeg.R/LowerLeg.R/foot.R",
            "Spine/Chest/Shoulder.L/UpperArm.L/LowerArm.L/Hand.L",
            "Spine/Chest/Shoulder.R/UpperArm.R/LowerArm.R/Hand.R",
            "Spine/Chest/Neck", "Spine/Chest/Shoulder.L", "Spine/Chest/Shoulder.R", "" };
        int count = rigPaths.Length * bonePaths.Length;
        _poseBones = new Transform[count];
        _boneRest = new Quaternion[count];
        _boneAxes = new Quaternion[count];
        _footRest = new Vector3[rigPaths.Length * 2];
        Quaternion worldToModel = Quaternion.Inverse(model.rotation);
        for (int rig = 0; rig < rigPaths.Length; rig++)
        {
            Transform hips = model.Find(rigPaths[rig] + "/Hips");
            if (hips == null) continue;
            for (int bone = 0; bone < bonePaths.Length; bone++)
            {
                Transform target = bone == HipsBone ? hips : hips.Find(bonePaths[bone]);
                if (target == null) continue;
                int index = rig * bonePaths.Length + bone;
                _poseBones[index] = target;
                _boneRest[index] = target.localRotation;
                _boneAxes[index] = worldToModel * target.rotation;
                if (bone == 8 || bone == 11) _footRest[rig * 2 + (bone == 11 ? 1 : 0)] = whale.InverseTransformPoint(target.position);
            }
        }

        // Keep each accessory's fitted pose while attaching its mesh and rig to the main head.
        Transform head = _poseBones[1];
        Transform hair = model.Find("头发");
        Transform fins = model.Find("鱼翅");
        if (head != null)
        {
            if (hair != null) hair.SetParent(head, true);
            if (fins != null) fins.SetParent(head, true);
        }
        Transform face = model.Find("Body");
        if (face != null) _face = face.GetComponent<SkinnedMeshRenderer>();
        if (_face != null && _face.sharedMesh != null)
        {
            Mesh mesh = _face.sharedMesh;
            _blinkShape = mesh.GetBlendShapeIndex("eye_close");
            _eyeHappyShape = mesh.GetBlendShapeIndex("eye_happy");
            _mouthShape = mesh.GetBlendShapeIndex("mouth_a");
            _smileShape = mesh.GetBlendShapeIndex("mouth_smile");
            if (_eyeHappyShape >= 0) _happyEyes = _face.GetBlendShapeWeight(_eyeHappyShape);
        }
        _nextBlink = Time.time + 1.4f;
        Debug.Log("[Dafeiyu] Kipfel motion ready. Retreat feet: " + (_poseBones[8] != null && _poseBones[11] != null)
            + ", fins attached to head: " + (fins != null && head != null && fins.parent == head)
            + ", skirt panels: " + (_skirtPanels == null ? 0 : _skirtPanels.Length));
    }

    private void InitializeSkirt(Transform model)
    {
        Transform skirt = model.Find("女仆装/armature.1/Hips/Dress_Root");
        if (skirt == null) return;
        int count = skirt.childCount;
        _skirtPanels = new Transform[count];
        _skirtRest = new Quaternion[count];
        _skirtLiftAxis = new Vector3[count];
        _skirtRadial = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            Transform panel = skirt.GetChild(i);
            if (!panel.name.StartsWith("Dress_")) continue;
            _skirtPanels[i] = panel;
            _skirtRest[i] = panel.localRotation;
            Vector3 radial = model.InverseTransformPoint(panel.position) - model.InverseTransformPoint(skirt.position);
            radial.y = 0f;
            radial = radial.normalized;
            Vector3 axis = model.TransformDirection(Vector3.Cross(radial, Vector3.up));
            _skirtLiftAxis[i] = panel.InverseTransformDirection(axis);
            _skirtRadial[i] = radial;
        }
    }

    private void PoseSkirt(float seat)
    {
        if (_skirtPanels == null) return;
        bool legsMoving = !_held && (_stepping || _walkBlend > 0f || _landingCompression > 0f);
        if (seat <= 0f && !legsMoving && !_skirtPosed) return;
        _skirtPosed = seat > 0f || legsMoving;
        float spread = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seat * 1.85f));
        Vector3 leftThigh = legsMoving ? ThighDirection(6) : Vector3.zero;
        Vector3 rightThigh = legsMoving ? ThighDirection(9) : Vector3.zero;
        for (int i = 0; i < _skirtPanels.Length; i++)
        {
            Transform panel = _skirtPanels[i];
            if (panel == null) continue;
            Vector3 radial = _skirtRadial[i];
            float seatLift = Mathf.Lerp(42f, 91f, Mathf.Clamp01(radial.z)) * spread;
            float walkLift = 0f;
            if (legsMoving)
            {
                float left = PanelLift(radial, leftThigh, -1f);
                float right = PanelLift(radial, rightThigh, 1f);
                walkLift = Mathf.Max(left, right) * (1f - spread);
            }
            panel.localRotation = _skirtRest[i] * Quaternion.AngleAxis(seatLift + walkLift, _skirtLiftAxis[i]);
        }
    }

    private Vector3 ThighDirection(int thighIndex)
    {
        Transform thigh = _poseBones[thighIndex];
        Transform knee = _poseBones[thighIndex + 1];
        if (thigh == null || knee == null) return Vector3.zero;
        return whale.InverseTransformDirection(knee.position - thigh.position).normalized;
    }

    private float PanelLift(Vector3 radial, Vector3 thigh, float side)
    {
        float alongPanel = Vector3.Dot(radial, thigh);
        float angle = Mathf.Atan2(alongPanel, Mathf.Max(0.15f, -thigh.y)) * Mathf.Rad2Deg;
        float sideWeight = Mathf.Clamp01(1f + radial.x * side);
        return Mathf.Clamp((angle - 10f) * 1.15f, 0f, 46f) * sideWeight;
    }

    private void AnimateKipfel()
    {
        float now = Time.time;
        float breath = Mathf.Sin(now * 1.95f);
        float sway = Mathf.Sin(now * 1.15f);
        float pat = ReactionPulse(now - _headPatStarted, 0.82f);
        float bump = ReactionPulse(now - _bumpStarted, 0.52f);
        float idleMotion = 1f - _carryBlend;
        float stepPhase = _stepping ? Mathf.Clamp01((now - _stepStarted) / _stepDuration) : 0f;
        float stepping = _stepping && !_approaching ? ReactionPulse(now - _stepStarted, _stepDuration) : 0f;
        float balance = stepping * Mathf.Sin(stepPhase * 3f * Mathf.PI);
        float recover = _stumbling ? ReactionPulse(stepPhase - 0.34f, 0.66f) : 0f;
        float mouth = 0f;
        if (voice.isPlaying && _speechEnvelope != null)
        {
            int frame = Mathf.Clamp(voice.timeSamples / SpeechWindow, 0, _speechEnvelope.Length - 1);
            mouth = Mathf.Clamp01(Mathf.Sqrt(_speechEnvelope[frame]) * 1.7f);
        }
        _mouthOpening = Mathf.MoveTowards(_mouthOpening, mouth, Time.deltaTime * (mouth > _mouthOpening ? 12f : 7f));
        float thinking = string.IsNullOrEmpty(_pendingChatId) ? 0f : 1f;
        float delta = Time.deltaTime;
        _touchPush = Vector3.SmoothDamp(_touchPush, Vector3.zero, ref _touchVelocity, 0.23f, 25f, delta);
        float leftPull = _gripWeight[0];
        float rightPull = _gripWeight[1];
        float seat = Mathf.SmoothStep(0f, 1f, _sitBlend);
        float armSwing = Mathf.Cos(_walkPhase * (2f * Mathf.PI)) * _walkBlend;

        Quaternion chest = Quaternion.Euler(1.7f * breath + 4f * pat - 17f * bump + 8f * seat + 7f * recover
            + _landingCompression * 125f,
            (_stumbling ? 0f : 2.3f * sway) + (leftPull - rightPull) * 7f + 4f * armSwing,
            1.6f * sway + 8f * balance + _touchPush.x + (rightPull - leftPull) * 4f);
        chest = Quaternion.Euler(_shoulderChestSwing - _shoulderHipSwing - Vector3.right * _touchPush.z) * chest;
        Quaternion head = Quaternion.Euler(1.6f * breath + 12f * pat - 7f * bump,
            _stumbling ? 0f : 3f * sway + 5f * thinking, 2f * sway + 7f * pat - 5f * thinking);
        Quaternion upperLeft = Quaternion.Euler(new Vector3(-1.7f * breath + 12f * armSwing, 2f * sway,
            -2f - 21f * bump - 7f * recover) * idleMotion + _limbSwing[0] + new Vector3(-22f, 0f, -6f) * seat);
        Quaternion upperRight = Quaternion.Euler(new Vector3(1.7f * breath - 12f * armSwing, -2f * sway,
            2f + 21f * bump + 7f * recover) * idleMotion + _limbSwing[1] + new Vector3(-22f, 0f, 6f) * seat);
        Quaternion foreLeft = Quaternion.Euler(new Vector3(-3f - 8f * bump, 0f, -2f)
            * idleMotion + _limbSwing[2] + Vector3.left * (23f * seat));
        Quaternion foreRight = Quaternion.Euler(new Vector3(-3f - 8f * bump, 0f, 2f)
            * idleMotion + _limbSwing[3] + Vector3.left * (23f * seat));
        Quaternion headLag = Quaternion.Euler(-_carryTilt * 0.24f);
        Quaternion headGrip = Quaternion.Euler(_headGripAngles);
        Quaternion hips = Quaternion.Euler(_shoulderHipSwing);
        Quaternion neck = Quaternion.Euler(_limbSwing[NeckSwing]);
        Quaternion leftShoulder = Quaternion.Euler(_limbSwing[LeftShoulderSwing]);
        Quaternion rightShoulder = Quaternion.Euler(_limbSwing[LeftShoulderSwing + 1]);
        Quaternion leftWrist = Quaternion.Euler(_limbSwing[LeftWristSwing]);
        Quaternion rightWrist = Quaternion.Euler(_limbSwing[LeftWristSwing + 1]);
        for (int rig = 0; rig < _poseBones.Length; rig += BonesPerRig)
        {
            PoseBone(rig + HipsBone, hips);
            PoseBone(rig, Quaternion.Slerp(Quaternion.identity, chest, idleMotion));
            Quaternion freeHead = Quaternion.Slerp(headLag, head, idleMotion);
            PoseBone(rig + 1, Quaternion.Slerp(freeHead, headGrip, _headGripWeight));
            PoseBone(rig + 2, upperLeft);
            PoseBone(rig + 3, upperRight);
            PoseBone(rig + 4, foreLeft);
            PoseBone(rig + 5, foreRight);
            PoseBone(rig + 12, leftWrist);
            PoseBone(rig + 13, rightWrist);
            PoseBone(rig + 14, neck);
            PoseBone(rig + LeftShoulderBone, leftShoulder);
            PoseBone(rig + LeftShoulderBone + 1, rightShoulder);
        }
        AnimateLegs(stepPhase, stepping);
        PoseSkirt(seat);
        if (_face == null) return;

        float blinkAge = now - _nextBlink;
        if (blinkAge >= 0.18f) _nextBlink = now + UnityEngine.Random.Range(2.8f, 5.3f);
        float blink = Mathf.Max(ReactionPulse(blinkAge, 0.18f), pat * 0.76f);
        if (_eyeHappyShape >= 0) _face.SetBlendShapeWeight(_eyeHappyShape, _happyEyes * (1f - blink));
        if (_blinkShape >= 0) _face.SetBlendShapeWeight(_blinkShape, blink * 100f);
        if (_mouthShape >= 0) _face.SetBlendShapeWeight(_mouthShape, Mathf.Max(_mouthOpening * 66f, bump * 12f));
        if (_smileShape >= 0) _face.SetBlendShapeWeight(_smileShape, pat * 38f);
    }

    private void AnimateLegs(float phase, float stepping)
    {
        float settling = _limbSwing[4].sqrMagnitude + _limbSwing[5].sqrMagnitude
            + _limbSwing[LeftKneeSwing].sqrMagnitude + _limbSwing[LeftKneeSwing + 1].sqrMagnitude
            + _limbSwing[LeftAnkleSwing].sqrMagnitude + _limbSwing[LeftAnkleSwing + 1].sqrMagnitude;
        bool planted = _sitBlend > 0f || _landingCompression > 0f
            || (_carryBlend <= 0f && _shoulderHipSwing.sqrMagnitude > 0.001f);
        bool walking = _walkBlend > 0f && !_held && _sitBlend <= 0f;
        bool active = _stepping || walking || planted || _carryBlend > 0f || settling > 0.01f;
        if (!active && !_legsWereStepping) return;

        // Lower the hips slightly while a foot is planted, so the leg can reach its target.
        if (tail == null && _stepping && !_approaching) whale.localPosition = _restPosition - Vector3.up * (0.052f * stepping);
        for (int rig = 0; rig < _poseBones.Length; rig += BonesPerRig)
        {
            ResetLeg(rig + 6);
            ResetLeg(rig + 9);
            int foot = rig / BonesPerRig * 2;
            if (walking && (_approaching || !_stepping))
            {
                WalkLeg(rig + 6, _footRest[foot], _walkPhase);
                WalkLeg(rig + 9, _footRest[foot + 1], Mathf.Repeat(_walkPhase + 0.5f, 1f));
            }
            else if (_stepping && !_approaching)
            {
                StepLeg(rig + 6, _footRest[foot], phase, _leftFootFirst);
                StepLeg(rig + 9, _footRest[foot + 1], phase, !_leftFootFirst);
            }
            else if (planted)
            {
                PlantFoot(rig + 6, _footRest[foot]);
                PlantFoot(rig + 9, _footRest[foot + 1]);
            }
            else if (active)
            {
                SwingLeg(rig + 6, 0);
                SwingLeg(rig + 9, 1);
            }
        }
        _legsWereStepping = active;
    }

    private void WalkLeg(int index, Vector3 rest, float phase)
    {
        Transform thigh = _poseBones[index];
        Transform shin = _poseBones[index + 1];
        Transform foot = _poseBones[index + 2];
        if (thigh == null || shin == null || foot == null) return;
        float sample = Mathf.Repeat(phase, 1f) * _walkAnkle.Length;
        int first = (int)sample;
        Vector3 ankle = Vector3.Lerp(_walkAnkle[first], _walkAnkle[(first + 1) % _walkAnkle.Length], sample - first)
            * _walkBlend;
        Quaternion footRotation = Quaternion.AngleAxis(ankle.z, whale.right) * foot.rotation;
        Vector3 target = transform.TransformPoint(_restPosition) + whale.TransformVector(rest);
        target += _stepDirection * ankle.x + Vector3.up * (ankle.y * 0.84f);
        ReachTarget(thigh, shin, foot, target, whale.forward);
        foot.rotation = footRotation;
    }

    private void PlantFoot(int index, Vector3 rest)
    {
        Transform thigh = _poseBones[index];
        Transform shin = _poseBones[index + 1];
        Transform foot = _poseBones[index + 2];
        if (thigh == null || shin == null || foot == null) return;
        Quaternion worldCorrection = whale.rotation * Quaternion.Inverse(Quaternion.Euler(_shoulderHipSwing))
            * Quaternion.Inverse(whale.rotation);
        Quaternion plantedRotation = worldCorrection * foot.rotation;
        Vector3 target = transform.TransformPoint(_restPosition) + whale.TransformVector(rest);
        float seat = Mathf.SmoothStep(0f, 1f, _sitBlend);
        float side = Mathf.Sign(rest.x);
        // Extend the feet as the hips descend so the knees stay below the front of the skirt.
        float reach = Mathf.Sqrt(seat) * SitFootReach;
        target += whale.forward * reach + (whale.right * (side * 0.014f) + Vector3.up * 0.012f) * seat;
        ReachTarget(thigh, shin, foot, target, whale.forward);
        foot.rotation = plantedRotation;
    }

    private void SwingLeg(int index, int side)
    {
        Vector3 knee = _limbSwing[LeftKneeSwing + side];
        knee.x = Mathf.Min(0f, knee.x);
        PoseBone(index, Quaternion.Euler(_limbSwing[4 + side]));
        PoseBone(index + 1, Quaternion.Euler(knee));
        PoseBone(index + 2, Quaternion.Euler(_limbSwing[LeftAnkleSwing + side]));
    }

    private void ResetLeg(int index)
    {
        if (_poseBones[index] != null) _poseBones[index].localRotation = _boneRest[index];
        if (_poseBones[index + 1] != null) _poseBones[index + 1].localRotation = _boneRest[index + 1];
        if (_poseBones[index + 2] != null) _poseBones[index + 2].localRotation = _boneRest[index + 2];
    }

    private void StepLeg(int index, Vector3 rest, float phase, bool leads)
    {
        Transform thigh = _poseBones[index];
        Transform shin = _poseBones[index + 1];
        Transform foot = _poseBones[index + 2];
        if (thigh == null || shin == null || foot == null) return;

        Quaternion footRotation = foot.rotation;
        float travel;
        float lift;
        if (leads)
        {
            float first = _stumbling ? 0.27f : 0.30f;
            float last = _stumbling ? 0.62f : 0.66f;
            travel = Mathf.SmoothStep(0f, _stumbling ? 0.53f : 0.44f, Mathf.Clamp01(phase / first))
                + Mathf.SmoothStep(0f, _stumbling ? 0.47f : 0.56f, Mathf.Clamp01((phase - last) / (1f - last)));
            lift = ReactionPulse(phase, first) * 0.062f + ReactionPulse(phase - last, 1f - last) * 0.042f;
        }
        else
        {
            float start = _stumbling ? 0.24f : 0.27f;
            float duration = _stumbling ? 0.40f : 0.43f;
            travel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - start) / duration));
            lift = ReactionPulse(phase - start, duration) * 0.079f;
        }
        float traveled = Vector3.Dot(transform.position - _stepStart, _stepDirection);
        float stride = Mathf.Clamp(travel * _stepDistance - traveled, -0.19f, 0.19f);
        stride *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - 0.94f) / 0.06f));
        Vector3 standingFoot = transform.TransformPoint(_restPosition) + whale.TransformVector(rest);
        Vector3 target = standingFoot + _stepDirection * stride + Vector3.up * lift;
        ReachTarget(thigh, shin, foot, target, whale.forward);
        foot.rotation = footRotation;
    }

    private void ReachTarget(Transform upperBone, Transform lowerBone, Transform end, Vector3 target, Vector3 bendDirection)
    {
        Vector3 origin = upperBone.position;
        Vector3 toTarget = target - origin;
        if (toTarget.sqrMagnitude < 0.00001f) return;
        float upper = Vector3.Distance(origin, lowerBone.position);
        float lower = Vector3.Distance(lowerBone.position, end.position);
        float reach = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upper - lower) + 0.001f, upper + lower - 0.001f);
        Vector3 axis = toTarget.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(bendDirection, axis);
        if (bend.sqrMagnitude < 0.0025f) bend = Vector3.ProjectOnPlane(whale.forward, axis);
        float jointAlong = (upper * upper + reach * reach - lower * lower) / (2f * reach);
        float jointOut = Mathf.Sqrt(Mathf.Max(0f, upper * upper - jointAlong * jointAlong));
        Vector3 joint = origin + axis * jointAlong + bend.normalized * jointOut;
        upperBone.rotation = Quaternion.FromToRotation(lowerBone.position - origin, joint - origin) * upperBone.rotation;
        Vector3 endpoint = origin + axis * reach;
        lowerBone.rotation = Quaternion.FromToRotation(end.position - lowerBone.position, endpoint - lowerBone.position) * lowerBone.rotation;
    }

    private void PoseBone(int index, Quaternion rotation)
    {
        Transform bone = _poseBones[index];
        if (bone == null) return;
        Quaternion axes = _boneAxes[index];
        bone.localRotation = _boneRest[index] * Quaternion.Inverse(axes) * rotation * axes;
    }

    private float ReactionPulse(float age, float duration)
    {
        if (age < 0f || age >= duration) return 0f;
        return Mathf.Sin(Mathf.PI * age / duration);
    }

    public override void PostLateUpdate()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) return;
        if (_chatOpen) FollowPlayerChat(player);
        if (_headCollider == null || !player.IsUserInVR()) return;
        if (_poseBones != null && _pickup != null)
        {
            UpdateHandInteraction(player);
        }
        Vector3 headCenter = transform.TransformPoint(_headCollider.center);
        if ((player.GetPosition() - headCenter).sqrMagnitude > 9f)
        {
            _touchingHead = false;
            return;
        }

        float radius = _headCollider.radius * transform.lossyScale.y + (_touchingHead ? 0.065f : 0.035f);
        bool left = HandTouchesHead(player, VRCPlayerApi.TrackingDataType.LeftHand, HumanBodyBones.LeftIndexDistal, headCenter, radius);
        bool right = HandTouchesHead(player, VRCPlayerApi.TrackingDataType.RightHand, HumanBodyBones.RightIndexDistal, headCenter, radius);
        if ((left || right) && !_touchingHead && Time.time >= _nextHeadPat)
        {
            _nextHeadPat = Time.time + 0.9f;
            _headPatStarted = Time.time;
            ShowReaction("嘿嘿，喜欢你摸摸头！");
            if (left) player.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 0.12f, 0.24f, 110f);
            if (right) player.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 0.12f, 0.24f, 110f);
            Debug.Log("[Dafeiyu] Head patted.");
        }
        _touchingHead = left || right;
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (!value || args.handType != HandType.LEFT) return;
        VRCPlayerApi player = Networking.LocalPlayer;
        if (Utilities.IsValid(player) && player.IsUserInVR() && !IsHandGripping(0)) ToggleChat(player);
    }

    private void ToggleChat(VRCPlayerApi player)
    {
        if (_chatPanel == null || Time.time < _nextChatToggle || chatInput.isFocused) return;
        _nextChatToggle = Time.time + 0.28f;
        _chatOpen = !_chatOpen;
        if (_chatOpen)
        {
            VRCPlayerApi.TrackingData head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            PositionChat(head.position, ChatHeading(player, head.rotation), player.IsUserInVR());
            _chatPanel.SetPositionAndRotation(_chatPosition, _chatRotation);
        }
        _chatPanel.gameObject.SetActive(_chatOpen);
        Debug.Log(_chatOpen ? "[Dafeiyu] Floating chat opened." : "[Dafeiyu] Floating chat closed.");
    }

    private Quaternion ChatHeading(VRCPlayerApi player, Quaternion viewRotation)
    {
        Vector3 forward = viewRotation * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.04f) return player.GetRotation();
        return Quaternion.LookRotation(forward, Vector3.up);
    }

    private void PositionChat(Vector3 headPosition, Quaternion heading, bool inVr)
    {
        _chatHeadAnchor = headPosition;
        _chatHeading = heading;
        Vector3 offset = inVr ? new Vector3(0.37f, -0.22f, 0.88f) : new Vector3(0f, -0.11f, 0.92f);
        _chatPosition = headPosition + heading * offset;
        // World-space UI is read and interacted with from its -Z side.
        _chatRotation = Quaternion.LookRotation(_chatPosition - headPosition, Vector3.up);
    }

    private void FollowPlayerChat(VRCPlayerApi player)
    {
        if (chatInput.isFocused) return;
        VRCPlayerApi.TrackingData head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Quaternion heading = ChatHeading(player, head.rotation);
        Vector3 movement = head.position - _chatHeadAnchor;
        if (movement.sqrMagnitude > ChatFollowDistance * ChatFollowDistance || Quaternion.Angle(heading, _chatHeading) > ChatTurnThreshold)
            PositionChat(head.position, heading, player.IsUserInVR());
        float blend = 1f - Mathf.Exp(-10f * Time.deltaTime);
        _chatPanel.SetPositionAndRotation(Vector3.Lerp(_chatPanel.position, _chatPosition, blend),
            Quaternion.Slerp(_chatPanel.rotation, _chatRotation, blend));
    }

    public override void InputGrab(bool value, UdonInputEventArgs args)
    {
        if (_poseBones == null || _pickup == null || _headCollider == null || _body == null) return;
        int hand = args.handType == HandType.LEFT ? 0 : 1;
        if (!value)
        {
            ReleaseHand(hand);
            return;
        }

        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || !player.IsUserInVR() || _held || IsHandGripping(hand)) return;
        Vector3 palm = PalmPosition(player, hand);
        if (palm == Vector3.zero || (palm - transform.position).sqrMagnitude > 2.25f) return;
        Vector3 bodyPoint = whale.InverseTransformPoint(palm);
        int armpit = bodyPoint.x < 0f ? 0 : 1;
        Transform upperArm = _poseBones[2 + armpit];
        Vector3 shoulderPoint = upperArm == null ? Vector3.zero : whale.InverseTransformPoint(upperArm.position);
        Vector3 shoulderReach = bodyPoint - shoulderPoint;
        bool atShoulder = upperArm != null && _poseBones[LeftShoulderBone + armpit] != null
            && Mathf.Abs(bodyPoint.x) >= 0.055f && shoulderReach.y >= -0.035f && shoulderReach.sqrMagnitude <= 0.0225f;
        Vector3 grip = shoulderPoint + new Vector3(armpit == 0 ? -0.05f : 0.05f, -0.075f, 0f);
        Vector3 reach = bodyPoint - grip;
        bool atArmpit = upperArm != null && Mathf.Abs(bodyPoint.x) >= 0.055f && shoulderReach.y < -0.035f
            && Mathf.Abs(reach.x) <= 0.14f && Mathf.Abs(reach.y) <= 0.11f && Mathf.Abs(reach.z) <= 0.16f;

        int wrist = -1;
        float nearest = 0.16f * 0.16f;
        if (atArmpit) nearest = Mathf.Min(nearest, reach.sqrMagnitude * 0.75f);
        if (atShoulder) nearest = Mathf.Min(nearest, shoulderReach.sqrMagnitude);
        for (int side = 0; side < 2; side++)
        {
            Transform bone = _poseBones[12 + side];
            Transform elbow = _poseBones[4 + side];
            if (bone == null || elbow == null || _grippedBy[side] >= 0) continue;
            Vector3 forearm = bone.position - elbow.position;
            Vector3 handCenter = bone.position + forearm.normalized * 0.035f;
            Vector3 center = whale.InverseTransformPoint(handCenter);
            if (bodyPoint.x * center.x < 0f || Mathf.Abs(bodyPoint.x) < 0.085f) continue;
            float distance = Mathf.Min((palm - handCenter).sqrMagnitude,
                (palm - (bone.position - forearm * 0.30f)).sqrMagnitude);
            if (distance >= nearest) continue;
            nearest = distance;
            wrist = side;
        }
        if (wrist >= 0)
        {
            _grippedBy[wrist] = hand;
            _wristTarget[wrist] = _poseBones[12 + wrist].position;
            ShowReaction("牵住我的手啦！");
            Haptic(player, hand, 0.25f);
            Debug.Log("[Dafeiyu] Hand grabbed.");
            return;
        }

        if (atShoulder)
        {
            if (_shoulderHeldBy[armpit] >= 0 || _armpitSide[1 - hand] == armpit) return;
            StopSteps();
            _shoulderHeldBy[armpit] = hand;
            _shoulderGripOffset[armpit] = bodyPoint;
            Haptic(player, hand, 0.25f);
            Debug.Log("[Dafeiyu] Shoulder grabbed: " + armpit);
            return;
        }

        Vector3 headCenter = transform.TransformPoint(_headCollider.center);
        if (_headHeldBy < 0 && (palm - headCenter).sqrMagnitude < 0.045f)
        {
            StopSteps();
            _headHeldBy = hand;
            _headGripOffset = bodyPoint;
            _headGripHandRotation = HeadGripHandRotation(player, hand);
            ShowReaction("慢一点，我会歪头的！");
            Haptic(player, hand, 0.25f);
            Debug.Log("[Dafeiyu] Head grabbed.");
            return;
        }

        if (!atArmpit || _armpitSide[1 - hand] == armpit || _shoulderHeldBy[armpit] >= 0) return;
        _armpitSide[hand] = armpit;
        _armpitGrip[hand] = grip;
        Haptic(player, hand, 0.22f);
        Debug.Log("[Dafeiyu] Armpit held. Both hands: " + (_armpitSide[1 - hand] >= 0));
    }

    private bool IsHandGripping(int hand)
    {
        return _armpitSide[hand] >= 0 || _headHeldBy == hand || _grippedBy[0] == hand || _grippedBy[1] == hand
            || _shoulderHeldBy[0] == hand || _shoulderHeldBy[1] == hand;
    }

    private bool UpperBodyHeld()
    {
        return _headHeldBy >= 0 || _shoulderHeldBy[0] >= 0 || _shoulderHeldBy[1] >= 0;
    }

    private void BeginCarry(VRCPlayerApi player, Vector3 left, Vector3 right)
    {
        _carryTarget = _body.position;
        _carryOffset[0] = _carryTarget - left;
        _carryOffset[1] = _carryTarget - right;
        _releaseVelocity = Vector3.zero;
        _carryRoll = 0f;
        _body.isKinematic = true;
        _manualCarry = true;
        OnPickup();
        Haptic(player, 0, 0.38f);
        Haptic(player, 1, 0.38f);
        Debug.Log("[Dafeiyu] Two-handed carry started.");
    }

    private void ReleaseHand(int hand)
    {
        for (int side = 0; side < 2; side++)
        {
            if (_grippedBy[side] == hand) _grippedBy[side] = -1;
            if (_shoulderHeldBy[side] == hand)
            {
                _shoulderHeldBy[side] = -1;
                Debug.Log("[Dafeiyu] Shoulder released: " + side);
            }
        }
        if (_headHeldBy == hand) _headHeldBy = -1;
        _armpitSide[hand] = -1;
        if (!_manualCarry) return;
        _manualCarry = false;
        _body.isKinematic = false;
        _body.velocity = Vector3.ClampMagnitude(_releaseVelocity, 1.5f);
        OnDrop();
    }

    private void UpdateHandInteraction(VRCPlayerApi player)
    {
        Vector3 left = PalmPosition(player, 0);
        Vector3 right = PalmPosition(player, 1);
        if (left == Vector3.zero || (left - transform.position).sqrMagnitude > 2.25f) ReleaseHand(0);
        if (right == Vector3.zero || (right - transform.position).sqrMagnitude > 2.25f) ReleaseHand(1);
        if (!_held)
        {
            if (_armpitSide[0] >= 0 && (left - whale.TransformPoint(_armpitGrip[0])).sqrMagnitude > 0.0625f) ReleaseHand(0);
            if (_armpitSide[1] >= 0 && (right - whale.TransformPoint(_armpitGrip[1])).sqrMagnitude > 0.0625f) ReleaseHand(1);
            if (_armpitSide[0] >= 0 && _armpitSide[1] >= 0) BeginCarry(player, left, right);
        }
        if (_manualCarry)
        {
            Vector3 leftTarget = left + _carryOffset[0];
            Vector3 rightTarget = right + _carryOffset[1];
            _carryTarget = (leftTarget + rightTarget) * 0.5f;
            float heightDifference = _armpitSide[0] == 0 ? rightTarget.y - leftTarget.y : leftTarget.y - rightTarget.y;
            _carryRoll = Mathf.Clamp(heightDifference * 115f, -22f, 22f);
        }

        float smoothing = 1f - Mathf.Exp(-24f * Time.deltaTime);
        for (int side = 0; side < 2; side++)
        {
            int owner = _grippedBy[side];
            _gripWeight[side] = Mathf.MoveTowards(_gripWeight[side], owner >= 0 ? 1f : 0f, Time.deltaTime * 8f);
            if (owner >= 0) _wristTarget[side] = Vector3.Lerp(_wristTarget[side], owner == 0 ? left : right, smoothing);
            if (_gripWeight[side] <= 0f) continue;
            for (int rig = 0; rig < _poseBones.Length; rig += BonesPerRig)
            {
                Transform upper = _poseBones[rig + 2 + side];
                Transform lower = _poseBones[rig + 4 + side];
                Transform wrist = _poseBones[rig + 12 + side];
                if (upper == null || lower == null || wrist == null) continue;
                Vector3 target = Vector3.Lerp(wrist.position, _wristTarget[side], _gripWeight[side]);
                Vector3 elbow = -whale.up + whale.right * (side == 0 ? -0.65f : 0.65f);
                ReachTarget(upper, lower, wrist, target, elbow);
            }
        }

        _headGripWeight = Mathf.MoveTowards(_headGripWeight, _headHeldBy >= 0 ? 1f : 0f, Time.deltaTime * 7f);
        if (_headHeldBy >= 0)
        {
            Vector3 offset = whale.InverseTransformPoint(_headHeldBy == 0 ? left : right) - _headGripOffset;
            Quaternion rotationDelta = HeadGripHandRotation(player, _headHeldBy) * Quaternion.Inverse(_headGripHandRotation);
            Vector3 handAngles = rotationDelta.eulerAngles;
            float pitch = Mathf.DeltaAngle(0f, handAngles.x) * 0.85f - offset.y * 150f;
            float yaw = Mathf.DeltaAngle(0f, handAngles.y) * 0.75f + offset.x * 145f;
            float roll = Mathf.DeltaAngle(0f, handAngles.z) * 0.5f - offset.x * 50f;
            Vector3 angles = new Vector3(Mathf.Clamp(pitch, -26f, 26f),
                Mathf.Clamp(yaw, -32f, 32f), Mathf.Clamp(roll, -12f, 12f));
            _headGripAngles = Vector3.Lerp(_headGripAngles, angles, smoothing);
        }
        else _headGripAngles = Vector3.Lerp(_headGripAngles, Vector3.zero, smoothing);
        UpdateBodyTouch(player, left, 0);
        UpdateBodyTouch(player, right, 1);
    }

    private void UpdateBodyTouch(VRCPlayerApi player, Vector3 palm, int hand)
    {
        Vector3 local = whale.InverseTransformPoint(palm);
        bool torso = local.y > 0.18f && local.y < 0.72f && Mathf.Abs(local.x) < 0.205f && Mathf.Abs(local.z) < 0.19f;
        bool arms = false;
        bool legs = false;
        for (int side = 0; side < 2; side++)
        {
            Transform upper = _poseBones[2 + side];
            Transform forearm = _poseBones[4 + side];
            Transform wrist = _poseBones[12 + side];
            if (upper != null && forearm != null && wrist != null)
                arms = arms || NearBone(palm, upper.position, forearm.position) || NearBone(palm, forearm.position, wrist.position);
            int leg = 6 + side * 3;
            Transform thigh = _poseBones[leg];
            Transform shin = _poseBones[leg + 1];
            Transform foot = _poseBones[leg + 2];
            if (thigh != null && shin != null && foot != null)
                legs = legs || NearBone(palm, thigh.position, shin.position) || NearBone(palm, shin.position, foot.position);
        }
        bool contact = torso || arms || legs;
        bool gripping = IsHandGripping(hand);
        if (contact && !_touchingBody[hand] && !gripping && Time.time >= _nextTouchFeedback)
        {
            _nextTouchFeedback = Time.time + 0.5f;
            _touchPush = new Vector3(local.x >= 0f ? 7f : -7f, 0f, local.z >= 0f ? 6f : -6f);
            Haptic(player, hand, 0.14f);
            ShowReaction(arms ? "碰到我的胳膊啦。" : (legs ? "碰到我的腿啦。" : "嘿，碰到我啦。"));
            Debug.Log("[Dafeiyu] Body touched.");
        }
        _touchingBody[hand] = contact;
    }

    private bool NearBone(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float phase = Mathf.Clamp01(Vector3.Dot(point - start, segment) / Mathf.Max(segment.sqrMagnitude, 0.00001f));
        return (point - (start + phase * segment)).sqrMagnitude < 0.0081f;
    }

    private Quaternion HeadGripHandRotation(VRCPlayerApi player, int hand)
    {
        Quaternion handRotation = player.GetTrackingData(hand == 0 ? VRCPlayerApi.TrackingDataType.LeftHand
            : VRCPlayerApi.TrackingDataType.RightHand).rotation;
        return Quaternion.Inverse(whale.rotation) * handRotation;
    }

    private Vector3 PalmPosition(VRCPlayerApi player, int hand)
    {
        Vector3 tracking = player.GetTrackingData(hand == 0 ? VRCPlayerApi.TrackingDataType.LeftHand
            : VRCPlayerApi.TrackingDataType.RightHand).position;
        Vector3 wrist = player.GetBonePosition(hand == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        Vector3 finger = player.GetBonePosition(hand == 0 ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        if (wrist == Vector3.zero || (wrist - tracking).sqrMagnitude > 0.08f) return tracking;
        if (finger == Vector3.zero || (finger - wrist).sqrMagnitude > 0.04f) return wrist;
        return Vector3.Lerp(wrist, finger, 0.65f);
    }

    private void Haptic(VRCPlayerApi player, int hand, float strength)
    {
        player.PlayHapticEventInHand(hand == 0 ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right,
            0.10f, strength, 105f);
    }

    private bool HandTouchesHead(VRCPlayerApi player, VRCPlayerApi.TrackingDataType hand, HumanBodyBones finger,
        Vector3 center, float radius)
    {
        Vector3 tracked = player.GetTrackingData(hand).position;
        if ((tracked - center).sqrMagnitude <= radius * radius) return true;
        Vector3 fingertip = player.GetBonePosition(finger);
        return fingertip != Vector3.zero && (fingertip - tracked).sqrMagnitude < 0.09f
            && (fingertip - center).sqrMagnitude <= radius * radius;
    }

    private void ShowReaction(string caption)
    {
        if (voice.isPlaying) return;
        subtitles.text = caption;
        _reactionCaptionUntil = Time.time + 2.5f;
    }

    private void PollReply()
    {
        bool pollChat = !string.IsNullOrEmpty(_pendingChatId);
        VRCUrl url = pollChat ? chatUrl : replyUrl;
        if (_downloading || Time.time < _nextPoll || VRCUrl.IsNullOrEmpty(url)) return;
        _downloading = true;
        _downloadingChat = pollChat;
        _nextPoll = Time.time + 6f;
        VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
    }

    public void SendChat()
    {
        if (chatInput == null || !string.IsNullOrEmpty(_pendingChatId)) return;
        string message = chatInput.text.Trim();
        if (message.Length == 0)
        {
            chatReply.text = "先输入一句话。";
            return;
        }
        _pendingChatId = Guid.NewGuid().ToString("N");
        _chatDeadline = Time.time + ChatTimeout;
        chatInput.interactable = false;
        chatSend.interactable = false;
        chatReply.text = "大肥鱼正在想怎么回你…";
        VRCPlayerApi player = Networking.LocalPlayer;
        if (Utilities.IsValid(player)) ReportWorldState(player, true);
        Debug.Log("[Dafeiyu input v1] " + _pendingChatId + " " + Convert.ToBase64String(Encoding.UTF8.GetBytes(message)));
        PollReply();
    }

    public void OnChatInputSelected()
    {
        Debug.Log("[Dafeiyu] Chat input selected.");
    }

    private void FinishChat(string message, bool success)
    {
        _pendingChatId = null;
        chatInput.interactable = true;
        chatSend.interactable = true;
        chatReply.text = message;
        if (success) chatInput.SetTextWithoutNotify("");
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        _downloading = false;
        byte[] payload = result.ResultBytes;
        int idEnd = FindNewline(payload, 0, 40);
        if (idEnd <= 0) return;

        int textStart = idEnd + 1;
        int textEnd = FindNewline(payload, textStart, textStart + 768);
        if (textEnd <= textStart) return;

        string replyId = Encoding.UTF8.GetString(payload, 0, idEnd);
        string replyText = Encoding.UTF8.GetString(payload, textStart, textEnd - textStart);
        if (!string.IsNullOrEmpty(_pendingChatId) && replyId == "!" + _pendingChatId)
        {
            FinishChat(replyText, false);
            return;
        }
        bool chatResponse = !string.IsNullOrEmpty(_pendingChatId) && replyId == _pendingChatId;
        if (chatResponse) FinishChat(replyText, true);
        if (!chatResponse && _downloadingChat) return;
        if (replyId == _lastReplyId) return;
        bool firstRead = string.IsNullOrEmpty(_lastReplyId);
        _lastReplyId = replyId;
        _lastSubtitle = replyText;
        _reactionCaptionUntil = 0f;
        subtitles.text = replyText;
        if (chatReply != null && string.IsNullOrEmpty(_pendingChatId) && !chatResponse) chatReply.text = replyText;
        if (firstRead) Debug.Log("[Dafeiyu] Connected to the local reply service.");
        if (replyId == "idle" || (firstRead && !chatResponse)) return;

        int audioStart = textEnd + 1;
        // Action replies add a versioned line before the PCM. Ordinary replies keep the original layout.
        if (payload.Length >= audioStart + 5 && payload[audioStart] == 'D' && payload[audioStart + 1] == 'F'
            && payload[audioStart + 2] == 'Y' && payload[audioStart + 3] == '1' && payload[audioStart + 4] == ' ')
        {
            int actionStart = audioStart + 5;
            int actionEnd = FindNewline(payload, actionStart, actionStart + 12);
            if (actionEnd > actionStart)
            {
                RunMovement(Encoding.UTF8.GetString(payload, actionStart, actionEnd - actionStart));
                audioStart = actionEnd + 1;
            }
        }
        int audioSize = payload.Length - audioStart;
        if (audioSize <= 0 || audioSize % 2 != 0 || audioSize > MaxSamples * 2) return;
        _pcm = new short[audioSize / 2];
        Buffer.BlockCopy(payload, audioStart, _pcm, 0, audioSize);
        _samples = new float[_pcm.Length];
        _nextSpeechEnvelope = new float[(_pcm.Length + SpeechWindow - 1) / SpeechWindow];
        _sampleOffset = 0;
    }

    private int FindNewline(byte[] payload, int start, int limit)
    {
        int end = Mathf.Min(payload.Length, limit);
        for (int index = start; index < end; index++)
        {
            if (payload[index] == 10) return index;
        }
        return -1;
    }

    private void DecodeVoiceFrame()
    {
        if (_pcm == null) return;
        int end = Mathf.Min(_sampleOffset + SamplesPerFrame, _pcm.Length);
        for (int index = _sampleOffset; index < end; index++)
        {
            float sample = _pcm[index] / 32768f;
            _samples[index] = sample;
            int frame = index / SpeechWindow;
            _nextSpeechEnvelope[frame] = Mathf.Max(_nextSpeechEnvelope[frame], Mathf.Abs(sample));
        }
        _sampleOffset = end;
        if (end < _pcm.Length) return;

        voice.Stop();
        voice.clip = null;
        if (_generatedClip != null) Destroy(_generatedClip);
        _generatedClip = AudioClip.Create("Dafeiyu reply", _samples.Length, 1, SampleRate, false);
        if (_generatedClip.SetData(_samples, 0))
        {
            voice.clip = _generatedClip;
            _speechEnvelope = _nextSpeechEnvelope;
            voice.Play();
            Debug.Log("[Dafeiyu] Playing a reply from the whale's AudioSource.");
        }
        _pcm = null;
        _samples = null;
        _nextSpeechEnvelope = null;
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        _downloading = false;
        _nextPoll = Time.time + 12f;
        if (!string.IsNullOrEmpty(_pendingChatId))
        {
            chatReply.text = "正在等待本机对话服务…";
        }
        if (string.IsNullOrEmpty(_lastReplyId))
        {
            subtitles.text = "嗨，我是大肥鱼。对话服务还没连上。";
        }
        Debug.LogWarning("[Dafeiyu] Reply download failed: " + result.ErrorCode + " " + result.Error);
    }
}
