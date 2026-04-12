using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;


#if UNITY_SWITCH
using nn.hid;
#endif

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    // NINTENDO STUFF
    private NpadId npadId = NpadId.Invalid;
    private NpadStyle npadStyle = NpadStyle.Invalid;
    private NpadState npadState = new NpadState();
    const float deadzone = 0.15f;
    bool updatePadState;

    public float moveSpeed = 6f;
    public float jumpHeight = 1.4f;
    public float gravity = -25f;
    public Transform cameraTransform;

    [Header("Rotation")]
    public float turnSpeed = 12f; // higher = snappier
    public bool snapTo8Directions = false;
    public float snapTurnSpeed = 30f; // how fast it snaps (if you want smoothing)

    CharacterController cc;
    float verticalVelocity;

    [Header("Animation (Legacy)")]
    public Animation anim;
    public string animIdle = "Idle";
    public string animWalk = "Walk";
    public string animJump = "Jump";
    public string animFall = "Falling";
    public float animFade = 0.1f;
    public float fallThreshold = -0.2f; // when to consider "falling"

    [Header("Hazards")]
    public bool canBeKnocked = true;
    float knockLockTimer = 0f;
    Vector3 hazardVelocity;

    [Header("Ladder")]
    public bool onLadder = false;
    public float climbSpeed = 4f;
    public bool lockHorizontalOnLadder = true;
    public float LastVerticalIntent { get; private set; }
    public bool IsGrounded => cc != null && cc.isGrounded;
    public float VerticalVelocity => verticalVelocity;

    public PlayerInteractor interactor;

    [Header("Anim Lock")]
    public float interactAnimLockTime = 0.25f;
    float animLockTimer = 0f;
    public string animInteract = "Attack_RightHand";


    [Header("Carry / Keychain")]
    public Transform carryRing;      // empty transform around player
    public int maxCarry = 3;
    public float ringRadius = 0.35f;
    public float ringHeight = 0.9f;
    public bool orbitItems = true;
    public float orbitSpeed = 90f;

    [Header("Water")]
    public bool inWater = false;                 // debug view
    public float waterMoveMultiplier = 0.5f;     // 0.5 = half speed
    public float waterGravityMultiplier = 0.35f; // 0.35 = floaty
    public float waterMaxFallSpeed = 8f;         // clamps falling speed in water (optional)
    public GameObject waterAudioObject;
    public AudioSource waterAudioSource;
    public AudioSource lavaAudioSource;

    int waterContacts = 0; // internal counter (supports overlapping triggers)
    public bool InWater => waterContacts > 0;

    readonly System.Collections.Generic.List<CarryableItem> carried
        = new System.Collections.Generic.List<CarryableItem>();

    float effectiveGravity;

    Vector3 lastMoveDir = Vector3.forward;
    public Vector3 LastMoveDir => lastMoveDir;

    string currentAnim; // NEW

    CarrySlots carrySlots;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if(anim == null) anim = GetComponentInChildren<Animation>();
        if(interactor == null) interactor = GetComponent<PlayerInteractor>();
#if UNITY_SWITCH
        Npad.Initialize();
        Npad.SetSupportedIdType(new NpadId[] { NpadId.Handheld, NpadId.No1 });
        Npad.SetSupportedStyleSet(NpadStyle.FullKey | NpadStyle.Handheld | NpadStyle.JoyDual);
#endif
        SetAnimationSpeeds();
        if(carryRing != null)
            carrySlots = carryRing.GetComponent<CarrySlots>();
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if(scene.buildIndex != 0)
        {
            PlayerPrefs.SetInt("currentLevel", scene.buildIndex);
            PlayerPrefs.Save();
#if !UNITY_EDITOR
            FsSaveDataPlayerPrefs.instance.SavePlayerPrefs();
#endif
        }
    }
    void SetAnimationSpeeds()
    {
        anim[animJump].speed = 0.5f;
        anim[animInteract].speed = 0.66f;
    }
    public void SetOnLadder(bool value)
    {
        onLadder = value;
        if(onLadder)
            verticalVelocity = 0f; // kill gravity drift when grabbing ladder
    }
    public void EnterWater()
    {
        waterContacts++;
        if(waterContacts < 0) waterContacts = 0;
        inWater = InWater;
        if(waterAudioSource != null) { waterAudioSource.Play(); }
    }

    public void ExitWater()
    {
        waterContacts--;
        if(waterContacts < 0) waterContacts = 0;
        inWater = InWater;
        if(waterAudioSource != null) { waterAudioSource.Stop(); }
    }
    void Update()
    {
#if UNITY_SWITCH
        updatePadState = UpdatePadState();

        AnalogStickState lStick = npadState.analogStickL;
        float lX = lStick.fx;
        float lY = lStick.fy;
#endif
        if(knockLockTimer > 0f)
        {
            knockLockTimer -= Time.deltaTime;

            // apply hazard movement
            cc.Move(hazardVelocity * Time.deltaTime);

            // still allow gravity
            float g = gravity * (InWater ? waterGravityMultiplier : 1f);
            verticalVelocity += g * Time.deltaTime;

            cc.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);

            return; // skip normal input while locked
        }
        // OLD INPUT (PC etc.)
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        bool interactPressed =
    Input.GetKeyDown(KeyCode.E);
        
#if UNITY_SWITCH
        interactPressed |= GetButtonDown(NpadButton.X); // or Y, your call
#endif

        if(interactPressed)
            interactor.TryInteract();
#if UNITY_SWITCH
        // If left stick is moved, use it instead (deadzone to avoid drift)
        float magSq = (lX * lX) + (lY * lY);
        if(magSq > deadzone * deadzone)
        {
            x = lX;
            z = lY;

            // Optional: clamp to unit length (safety)
            float mag = Mathf.Sqrt(magSq);
            if(mag > 1f)
            {
                x /= mag;
                z /= mag;
            }
        }
#endif
        LastVerticalIntent = z;

        Vector3 moveDir = new Vector3(x, 0f, z);
        if(moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        if(cameraTransform != null)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();

            moveDir = (right * x + forward * z);
            if(moveDir.sqrMagnitude > 1f) moveDir.Normalize();
        }





        // ===== LADDER MODE =====
        if(onLadder)
        {
            // Use forward/back input (z) as climb (W/S or stick up/down)
            float climb = Mathf.Max(0f, z); // UP ONLY

            // Optional deadzone on keyboard not needed, but for stick it helps
            if(Mathf.Abs(climb) < 0.1f) climb = 0f;

            // Cancel gravity while on ladder
            verticalVelocity = 0f;

            // Optional: lock horizontal movement while climbing
            Vector3 ladderMove = Vector3.zero;

            if(!lockHorizontalOnLadder)
            {
                // allow some horizontal drift if you want
                ladderMove = moveDir * moveSpeed;
            }

            ladderMove.y = climb * climbSpeed;

            // Move
            cc.Move(ladderMove * Time.deltaTime);

            // Optional anim: treat as walk when climbing
            if(Mathf.Abs(climb) > 0.01f) PlayAnim(animWalk);
            else PlayAnim(animIdle);

            // Optional: press Jump to hop off ladder
            if(Input.GetButtonDown("Jump"))
            {
                SetOnLadder(false);
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                PlayAnimImmediate(animJump);
            }

#if UNITY_SWITCH
            if(GetButtonDown(NpadButton.A))
            {
                SetOnLadder(false);
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                PlayAnimImmediate(animJump);
            }
#endif

            return; // IMPORTANT: skip normal gravity/grounding path
        }


        float effectiveGravity = gravity * (InWater ? waterGravityMultiplier : 1f);
        float effectiveMoveSpeed = moveSpeed * (InWater ? waterMoveMultiplier : 1f);


        bool isMoving = moveDir.sqrMagnitude > 0.001f; // NEW


        Vector3 flat = moveDir;
        flat.y = 0f;

        if(flat.sqrMagnitude > 0.001f)
            lastMoveDir = flat.normalized;

        // --- Rotate towards movement direction ---
        Vector3 flatMoveDir = moveDir;
        flatMoveDir.y = 0f;

        if(flatMoveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot;

            if(snapTo8Directions)
            {
                float yaw = Mathf.Atan2(flatMoveDir.x, flatMoveDir.z) * Mathf.Rad2Deg;
                float snappedYaw = Mathf.Round(yaw / 45f) * 45f;
                targetRot = Quaternion.Euler(0f, snappedYaw, 0f);

                // Option A: instant snap
                transform.rotation = targetRot;

                // Option B: snappy but still smooth
                //transform.rotation = Quaternion.RotateTowards(
                //    transform.rotation,
                //    targetRot,
                //    snapTurnSpeed * Time.deltaTime
                //);
            }
            else
            {
                targetRot = Quaternion.LookRotation(flatMoveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    turnSpeed * Time.deltaTime
                );
            }
        }

        // --- Grounding ---
        if(cc.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        // --- Jump detection ---
        bool jumpedThisFrame = false;

        if(Input.GetButtonDown("Jump") && cc.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * effectiveGravity);
            jumpedThisFrame = true;
        }
#if UNITY_SWITCH
        if(cc.isGrounded && GetButtonDown(NpadButton.A))
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * effectiveGravity);
            jumpedThisFrame = true;
        }
#endif

        if(jumpedThisFrame)
        {
            PlayAnimImmediate(animJump);
        }


        // --- Gravity ---
        verticalVelocity += effectiveGravity * Time.deltaTime;

        // Optional: clamp fall speed in water so you don't plummet like a brick
        if(InWater && verticalVelocity < -waterMaxFallSpeed)
            verticalVelocity = -waterMaxFallSpeed;

        // --- Choose locomotion anim (priority: fall > walk > idle; jump handled above) ---
        // tick anim lock
        if(animLockTimer > 0f)
            animLockTimer -= Time.deltaTime;

        // --- Choose locomotion anim (priority: fall > walk > idle; jump handled above) ---
        if(!jumpedThisFrame && animLockTimer <= 0f)
        {
            bool grounded = cc.isGrounded;
            bool falling = !grounded && verticalVelocity < fallThreshold;

            if(falling) PlayAnim(animFall);
            else if(grounded)
            {
                if(isMoving) PlayAnim(animWalk);
                else PlayAnim(animIdle);
            }
        }

        // --- Apply movement ---

        Vector3 velocity = moveDir * effectiveMoveSpeed;
        velocity.y = verticalVelocity;

        cc.Move(velocity * Time.deltaTime);

        if(orbitItems && carryRing != null && carried.Count > 0)
        {
            carryRing.Rotate(0f, orbitSpeed * Time.deltaTime, 0f, Space.Self);
        }
        bool dropPressed = Input.GetKeyDown(KeyCode.Q);
#if UNITY_SWITCH
        dropPressed |= GetButtonDown(NpadButton.Y); // pick any button
#endif

        if(dropPressed)
            DropLastItem();
    }

    public bool DropLastItem(float forwardOffset = 0.8f, float upOffset = 0.2f)
    {
        if(carried.Count == 0) return false;

        var item = carried[carried.Count - 1];
        carried.RemoveAt(carried.Count - 1);

        if(item == null) return false;

        item.isCarried = false;

        // Unparent
        item.transform.SetParent(null, worldPositionStays: true);

        // Place it slightly in front of the player
        Vector3 dropPos = transform.position + transform.forward * forwardOffset + Vector3.up * upOffset;
        item.transform.position = dropPos;

        // Re-enable collider
        var col = item.GetComponent<Collider>();
        if(col) col.enabled = true;

        // Re-enable rigidbody if present (optional)
        var rb = item.GetComponent<Rigidbody>();
        if(rb)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        RepositionCarried(); // if you have ring layout
        return true;
    }


    public bool PickupItem(CarryableItem item)
    {
        if(item == null) return false;
        if(carried.Count >= maxCarry) return false;

        if(carryRing == null)
        {
            Debug.LogWarning("No carryRing assigned on player.");
            return false;
        }

        carried.Add(item);
        item.isCarried = true;

        // Disable physics/collisions while carried
        var rb = item.GetComponent<Rigidbody>();
        if(rb) { rb.isKinematic = true; rb.useGravity = false; }

        var col = item.GetComponent<Collider>();
        if(col) col.enabled = false;

        // Parent to ring and reposition
        item.transform.SetParent(carryRing, worldPositionStays: false);
        RepositionCarried();

        return true;
    }

    public bool HasItem(string requiredId, CarryType requiredType = CarryType.Generic)
    {
        for(int i = 0; i < carried.Count; i++)
        {
            var it = carried[i];
            if(it == null) continue;

            bool typeOk = (requiredType == CarryType.Generic) || (it.type == requiredType);
            bool idOk = string.IsNullOrEmpty(requiredId) || it.itemId == requiredId;

            if(typeOk && idOk) return true;
        }
        return false;
    }

    public bool ConsumeItem(string requiredId, CarryType requiredType = CarryType.Generic)
    {
        for(int i = 0; i < carried.Count; i++)
        {
            var it = carried[i];
            if(it == null) continue;

            bool typeOk = (requiredType == CarryType.Generic) || (it.type == requiredType);
            bool idOk = string.IsNullOrEmpty(requiredId) || it.itemId == requiredId;

            if(typeOk && idOk)
            {
                carried.RemoveAt(i);

                // Destroy (or drop) � choose one
                Destroy(it.gameObject);

                RepositionCarried();
                return true;
            }
        }
        return false;
    }

    void RepositionCarried()
    {
        int n = carried.Count;
        if(n == 0) return;

        // If slots exist, snap each item to a slot transform.
        if(carrySlots != null && carrySlots.slots != null && carrySlots.slots.Length > 0)
        {
            for(int i = 0; i < n; i++)
            {
                var it = carried[i];
                if(it == null) continue;

                Transform slot = carrySlots.GetSlot(i);
                if(slot == null) continue;

                it.transform.SetParent(slot, worldPositionStays: false);
                it.transform.localPosition = Vector3.zero;

                // Apply item-specific carry pose
                it.transform.localRotation = Quaternion.Euler(it.carriedLocalRotation);
                it.transform.localScale = it.carriedLocalScale;

            }
            return;
        }

        // Fallback (if you forget to assign slots): your old ring layout.
        for(int i = 0; i < n; i++)
        {
            var it = carried[i];
            if(it == null) continue;

            float angle = (i / (float)n) * Mathf.PI * 2f;
            Vector3 localPos = new Vector3(
                Mathf.Cos(angle) * ringRadius,
                ringHeight,
                Mathf.Sin(angle) * ringRadius
            );
            it.transform.SetParent(carryRing, worldPositionStays: false);
            it.transform.localPosition = localPos;
            it.transform.localRotation = Quaternion.identity;
        }
    }




    // ===== Legacy Animation helpers =====
    void PlayAnim(string clipName)
    {
        if(anim == null) return;
        if(string.IsNullOrEmpty(clipName)) return;
        if(currentAnim == clipName) return;
        if(anim.GetClip(clipName) == null) return;

        anim.CrossFade(clipName, animFade);
        currentAnim = clipName;
    }
//    void TryInteract()
//    {
//        Vector3 origin = transform.position + Vector3.up * 0.5f;
//        Vector3 dir = transform.forward;

//        animLockTimer = interactAnimLockTime;

//        bool didHitInteractable = false;

//#if UNITY_EDITOR
//        DebugDrawSphereCast(origin, dir, 0.5f, interactRange, Color.cyan);
//#endif

//        // If you ONLY want the anim when you hit something, we delay playing it until we confirm hit.
//        // If you want it always (even on air), we can play it immediately.
//        if(!playInteractAnimOnlyOnHit && !string.IsNullOrEmpty(animInteract))
//            PlayAnimImmediate(animInteract);

//        if(Physics.SphereCast(origin, 0.5f, dir, out RaycastHit hit, interactRange, interactMask))
//        {

//            // Get ALL interactables in the hit collider's parent chain
//            var interactables = hit.collider.GetComponentsInParent<IInteractable>(includeInactive: false);

//            if(interactables != null && interactables.Length > 0)
//            {
//                // De-dupe just in case (child collider + parent collider setups can cause repeats)
//                var seen = new HashSet<IInteractable>();

//                for(int i = 0; i < interactables.Length; i++)
//                {
//                    var ia = interactables[i];
//                    if(ia == null) continue;
//                    if(!seen.Add(ia)) continue;

//                    didHitInteractable = true;

//                    // If you only want the anim on hit, play it once when we confirm we have something
//                    if(playInteractAnimOnlyOnHit && !string.IsNullOrEmpty(animInteract))
//                        PlayAnimImmediate(animInteract);

//                    ia.Interact(this);
//                }
//            }
//        }

//        // If you want the anim even when you miss (pressing air) AND you didn't already play it earlier
//        if(playInteractAnimOnlyOnHit && !didHitInteractable)
//        {
//            if(!string.IsNullOrEmpty(animInteract))
//                PlayAnimImmediate(animInteract);
//        }
//    }

    public void PlayInteractAnim()
    {
        Debug.Log("PLAYING INTERACT ANIM");
        animLockTimer = interactAnimLockTime;
        if(!string.IsNullOrEmpty(animInteract))
            PlayAnimImmediate(animInteract);
    }
    void PlayAnimImmediate(string clipName)
    {
        if(anim == null) return;
        if(string.IsNullOrEmpty(clipName)) return;
        if(anim.GetClip(clipName) == null) return;
        
        anim.CrossFade(clipName, animFade);
        anim[clipName].time = 0f; // restart
        currentAnim = clipName;
    }
    public void ApplyHazardKnockback(Vector3 dir, float strength, float upBoost, float lockTime, string source = "fire")
    {
        if(!canBeKnocked) return;

        hazardVelocity = dir * strength;
        verticalVelocity = Mathf.Max(verticalVelocity, upBoost); // small pop up
        knockLockTimer = lockTime;

        if (source=="fire")
        {
            if (lavaAudioSource != null) { lavaAudioSource.Play(); }
        }

        // Optional: play a reaction anim if you have one
        PlayAnim("Hit");
    }
    public bool GetButtonDown(NpadButton npadButton)
    {
            // Use the cached state
            if(updatePadState)
            {
                return npadState.GetButtonDown(npadButton);
            }
            else
            {
                return false;
            }
        
    }
    private bool UpdatePadState()
    {
        NpadStyle handheldStyle = Npad.GetStyleSet(NpadId.Handheld);
        NpadState handheldState = npadState;
        if(handheldStyle != NpadStyle.None)
        {
            Npad.GetState(ref handheldState, NpadId.Handheld, handheldStyle);
            if(handheldState.buttons != NpadButton.None)
            {
                npadId = NpadId.Handheld;
                npadStyle = handheldStyle;
                npadState = handheldState;
                return true;
            }
        }

        NpadStyle no1Style = Npad.GetStyleSet(NpadId.No1);
        NpadState no1State = npadState;
        if(no1Style != NpadStyle.None)
        {
            Npad.GetState(ref no1State, NpadId.No1, no1Style);
            if(no1State.buttons != NpadButton.None)
            {
                npadId = NpadId.No1;
                npadStyle = no1Style;
                npadState = no1State;
                return true;
            }
        }

        if((npadId == NpadId.Handheld) && (handheldStyle != NpadStyle.None))
        {
            npadId = NpadId.Handheld;
            npadStyle = handheldStyle;
            npadState = handheldState;
        }
        else if((npadId == NpadId.No1) && (no1Style != NpadStyle.None))
        {
            npadId = NpadId.No1;
            npadStyle = no1Style;
            npadState = no1State;
        }
        else
        {
            npadId = NpadId.Invalid;
            npadStyle = NpadStyle.Invalid;
            npadState.Clear();
            return false;
        }
        return true;
    }
    public bool GetButtonUp(NpadButton npadButton)
    {
       
            if(updatePadState)
            {
                return npadState.GetButtonUp(npadButton);
            }
            else
            {
                return false;
            }

    }


}
