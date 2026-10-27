using System;
using UnityEngine;

namespace CoreDump
{
    // Fast arcade movement on a Rigidbody, in the spirit of Titanfall and Ultrakill. WASD steers,
    // parts push the robot (shot kicks, Jets, Springs, Rams, Grapples), and speed carries.
    // How well it walks depends on the Legs and Treads that touch the ground; none means a slow shuffle.
    // Legs also give the slide (hold Ctrl at speed) and, with two or more, the wall-run (jump into a
    // wall while moving fast).
    // Speed from pushes ("momentum") is kept apart from walking speed: walking starts and stops
    // crisply, while momentum fades slowly and is lost against walls. Landing turns any speed above
    // walking speed into momentum, so jumping again right away keeps it (bunny hopping).
    // Runs after the parts each physics step, so a held Jet's push lands in the same step.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody), typeof(RobotBody))]
    public class RobotMotor : MonoBehaviour
    {
        [Header("Walking (from Legs and Treads that touch the ground)")]
        [Tooltip("With no working Legs or Treads, so you're never completely stuck.")]
        public float shuffleSpeed = 3f;
        [Tooltip("With one Leg, which moves you in little hops.")]
        public float limpSpeed = 9f;
        public float limpHopSpeed = 5f;
        public float limpHopInterval = 0.28f;
        [Tooltip("With two Legs.")]
        public float legSpeed = 16f;
        [Tooltip("What the third Leg adds. Each one after that adds a bit less.")]
        public float extraLegSpeed = 3f;
        public float maxWalkSpeed = 26f;
        public float legAcceleration = 160f;
        [Tooltip("Treads drive faster than Legs, but are slow to get going and skid when they stop.")]
        public float treadSpeed = 21f;
        public float treadAcceleration = 30f;
        public float treadBraking = 14f;
        public float turnSpeed = 1080f;

        [Header("Air")]
        [Tooltip("Steering in the air. Turning in the air keeps your speed; it only adds speed up to this.")]
        public float airSpeed = 12f;
        public float airAcceleration = 45f;

        [Header("Momentum from parts")]
        [Tooltip("How quickly speed from pushes fades on the ground, in meters per second squared.")]
        public float groundMomentumDrag = 14f;
        [Tooltip("How quickly speed from pushes fades in the air.")]
        public float airMomentumDrag = 1f;
        [Tooltip("For this long after landing, nothing slows you, so jumping right away keeps all your speed.")]
        public float landingGrace = 0.12f;

        [Header("Slide (needs Legs)")]
        public float slideMinSpeed = 9f;
        [Tooltip("Speed added when a slide starts. Only once per cooldown, so slide-spamming doesn't build speed forever.")]
        public float slideBoost = 6f;
        public float slideBoostCooldown = 0.9f;
        public float slideFriction = 4f;
        [Tooltip("How quickly a slide turns toward the keys you hold, in degrees per second.")]
        public float slideTurnRate = 70f;
        public float slideStopSpeed = 4f;

        [Header("Wall-run (needs two Legs)")]
        public float wallRunMinSpeed = 9f;
        public float wallRunSpeed = 16f;
        public float wallRunTime = 1.6f;
        [Tooltip("Share of gravity while wall-running.")]
        public float wallRunGravity = 0.12f;
        [Tooltip("Upward speed a wall-run starts with.")]
        public float wallRunLift = 1.5f;
        [Tooltip("The fastest a wall-run sinks, in meters per second.")]
        public float wallRunSink = 0.6f;
        [Tooltip("How far past the robot's side a wall still counts.")]
        public float wallReach = 0.9f;

        [Header("Gravity")]
        public float gravity = 32f;
        public float fallGravityMultiplier = 1.4f;
        public float maxFallSpeed = 50f;

        public bool IsGrounded { get; private set; }
        public bool IsSliding { get; private set; }
        public bool IsWallRunning { get; private set; }
        // Points away from the wall being run on.
        public Vector3 WallNormal { get; private set; }
        public Vector3 Velocity => body.linearVelocity;
        public float HorizontalSpeed => new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
        public int WorkingLegs { get; private set; }
        // How fast the robot was falling when it last touched down, in meters per second.
        public float LandingSpeed { get; private set; }
        // World height of the floor the robot last stood on.
        public float FloorHeight { get; private set; } = float.NegativeInfinity;
        public bool HasTreads { get; private set; }
        // For the test HUD: how the robot walks right now.
        public string WalkMode { get; private set; } = "";
        // Fires on the step the robot touches down. Everything that resets on landing listens to it.
        public event Action Landed;

        Rigidbody body;
        RobotBody robot;
        Vector2 moveInput;
        bool slideHeld;
        float targetYaw;
        bool touchingGround;
        Vector3 groundNormal = Vector3.up;
        Vector3 contactNormal = Vector3.up;
        float jumpHeight;
        Vector3 springPush;
        Vector3 thrust;
        Vector3 lunge;
        float lungeTime;
        float lungeKeep = 1f;
        float endKeep = 1f;
        bool lungeEnding;
        float steerLockedUntil;
        float nextLimpHop;
        float landedAt = -1f;
        // The fastest the robot has fallen since it left the ground.
        float fallSpeed;
        float nextSlideBoost;
        float wallRunEnds;
        float nextWallRun;
        // The wall last run on. It can't be run on again until you land, so a run can't go on forever.
        Vector3 lastWall;
        // The horizontal part of the velocity that came from pushes, on top of walking.
        Vector3 momentum;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            robot = GetComponent<RobotBody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            targetYaw = transform.eulerAngles.y;
        }

        public void SetMoveInput(Vector2 input) => moveInput = Vector2.ClampMagnitude(input, 1f);
        public void SetSlideInput(bool held) => slideHeld = held;
        public void SetFacing(float yaw) => targetYaw = yaw;

        // A jump off the ground with Springs: their heights add up into one jump.
        public void Jump(float height) => jumpHeight += height;

        // The upward speed that reaches `height`.
        public float JumpSpeed(float height) => Mathf.Sqrt(2f * gravity * Mathf.Max(0f, height));

        // A Spring kicking off a wall or a slope: an instant push, added up across Springs.
        public void SpringOff(Vector3 velocity) => springPush += velocity;

        // An instant change in velocity, like a Jet's dash or a part's burst.
        public void Push(Vector3 velocityChange)
        {
            body.linearVelocity += velocityChange;
            momentum += new Vector3(velocityChange.x, 0f, velocityChange.z);
        }

        // A push from a gun's kick or an explosion. Treads can't be knocked back, even by your own guns.
        public void Kick(Vector3 velocityChange)
        {
            if (!HasTreads) Push(velocityChange);
        }

        // A steady push for this physics step, like a held Jet.
        public void Thrust(Vector3 acceleration) => thrust += acceleration;

        // A burst that replaces the robot's velocity, and stops WASD steering for `steerLock` seconds.
        // When it ends, only `keep` of its speed carries on. Rams and Jet dashes firing together add
        // their bursts up into one.
        public void Lunge(Vector3 velocity, float steerLock, float keep = 1f)
        {
            lunge += velocity;
            lungeTime = Mathf.Max(lungeTime, steerLock);
            lungeKeep = Mathf.Min(lungeKeep, keep);
        }

        public bool IsLunging => Time.time < steerLockedUntil;

        public void EndWallRun()
        {
            if (!IsWallRunning) return;
            IsWallRunning = false;
            lastWall = WallNormal;
            nextWallRun = Time.time + 0.25f;
        }

        // A jump pad or launch pad: replaces the robot's velocity outright, so every launch from the
        // same pad flies the same however you came in. You can still steer in the air.
        public void Launch(Vector3 velocity)
        {
            body.linearVelocity = velocity;
            momentum = new Vector3(velocity.x, 0f, velocity.z);
            IsSliding = false;
            EndWallRun();
        }

        public void Teleport(Vector3 position)
        {
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector3.zero;
            momentum = Vector3.zero;
            fallSpeed = 0f;
            IsSliding = false;
            IsWallRunning = false;
        }

        void FixedUpdate()
        {
            bool wasGrounded = IsGrounded;
            IsGrounded = touchingGround && body.linearVelocity.y <= 0.5f;
            groundNormal = IsGrounded ? contactNormal : Vector3.up;
            touchingGround = false;
            contactNormal = Vector3.up;
            if (IsGrounded && !wasGrounded) Land();
            if (IsGrounded) FloorHeight = transform.position.y + robot.BottomHeight;

            WalkProfile(out float speed, out float acceleration, out float braking, out bool limping);
            Turn();
            EndLunge();
            ApplyLunge();
            UpdateWallRun();
            if (!IsLunging)
            {
                if (IsWallRunning) WallRun();
                else Move(speed, acceleration, braking, limping);
            }
            ApplyGravity();
            ApplyPushes();
            if (!IsGrounded) fallSpeed = Mathf.Max(fallSpeed, -body.linearVelocity.y);
        }

        void Land()
        {
            landedAt = Time.time;
            IsWallRunning = false;
            lastWall = Vector3.zero;
            // Speed above walking speed becomes momentum, which fades slowly instead of being braked.
            WalkProfile(out float speed, out _, out _, out _);
            var horizontal = Flat(body.linearVelocity);
            var walk = horizontal - momentum;
            if (walk.magnitude > speed) momentum += walk - walk.normalized * speed;
            LandingSpeed = fallSpeed;
            fallSpeed = 0f;
            Landed?.Invoke();
        }

        void Turn()
        {
            float yaw = Mathf.MoveTowardsAngle(body.rotation.eulerAngles.y, targetYaw, turnSpeed * Time.fixedDeltaTime);
            body.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
        }

        // When a burst's steering lock runs out, it sheds what it doesn't keep.
        void EndLunge()
        {
            if (!lungeEnding || IsLunging) return;
            lungeEnding = false;
            var v = body.linearVelocity;
            body.linearVelocity = new Vector3(v.x * endKeep, v.y, v.z * endKeep);
            momentum *= endKeep;
        }


        void ApplyLunge()
        {
            if (lunge == Vector3.zero) return;
            body.linearVelocity = lunge;
            momentum = Flat(lunge);
            steerLockedUntil = Time.time + lungeTime;
            endKeep = lungeKeep;
            lungeEnding = true;
            lungeKeep = 1f;
            IsSliding = false;
            EndWallRun();
            lunge = Vector3.zero;
            lungeTime = 0f;
        }

        void Move(float speed, float acceleration, float braking, bool limping)
        {
            var wish = Quaternion.Euler(0f, targetYaw, 0f) * new Vector3(moveInput.x, 0f, moveInput.y);
            var v = body.linearVelocity;
            var horizontal = Flat(v);
            float dt = Time.fixedDeltaTime;

            // Walls eat momentum: never keep more of it than the robot is actually moving.
            if (momentum.sqrMagnitude > 0f)
            {
                var direction = momentum.normalized;
                momentum = direction * Mathf.Clamp(Vector3.Dot(horizontal, direction), 0f, momentum.magnitude);
            }
            var walk = horizontal - momentum;

            UpdateSlide(horizontal, ref walk);
            bool inGrace = Time.time < landedAt + landingGrace;

            if (IsSliding)
            {
                // Everything is momentum while sliding: it turns slowly toward the keys, and slopes speed it up.
                if (wish.sqrMagnitude > 0.01f)
                    momentum = Vector3.RotateTowards(momentum, wish.normalized * momentum.magnitude, slideTurnRate * Mathf.Deg2Rad * dt, 0f);
                var downhill = Flat(Vector3.ProjectOnPlane(Vector3.down, groundNormal));
                momentum += downhill * gravity * dt;
                momentum = Vector3.MoveTowards(momentum, Vector3.zero, slideFriction * dt);
            }
            else if (IsGrounded)
            {
                if (!inGrace) walk = Vector3.MoveTowards(walk, WalkTarget(wish, speed), (wish.sqrMagnitude > 0.01f ? acceleration : braking) * dt);
                // One Leg gets you around in little hops.
                if (limping && wish.sqrMagnitude > 0.01f && Time.time >= nextLimpHop)
                {
                    v.y = Mathf.Max(v.y, limpHopSpeed);
                    nextLimpHop = Time.time + limpHopInterval;
                }
                if (!inGrace) momentum = Vector3.MoveTowards(momentum, Vector3.zero, groundMomentumDrag * dt);
            }
            else
            {
                // In the air, letting go of the keys keeps your speed, and turning keeps it too.
                if (wish.sqrMagnitude > 0f)
                    walk = Vector3.MoveTowards(walk, WalkTarget(wish, Mathf.Max(airSpeed, walk.magnitude)), airAcceleration * dt);
                momentum = Vector3.MoveTowards(momentum, Vector3.zero, airMomentumDrag * dt);
            }

            horizontal = walk + momentum;
            body.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
        }

        // Walking adds speed only up to `speed` along the way you're heading, counting the momentum
        // already going that way, so momentum and walking never stack into runaway speed. Sideways
        // you can still steer at full walking speed.
        Vector3 WalkTarget(Vector3 wish, float speed)
        {
            if (wish.sqrMagnitude < 0.0001f) return Vector3.zero;
            float alreadyGoing = Vector3.Dot(momentum, wish.normalized);
            return wish * Mathf.Clamp(speed - alreadyGoing, 0f, speed);
        }

        void UpdateSlide(Vector3 horizontal, ref Vector3 walk)
        {
            bool canSlide = IsGrounded && slideHeld && WorkingLegs > 0;
            if (!IsSliding && canSlide && horizontal.magnitude >= slideMinSpeed)
            {
                IsSliding = true;
                // All the speed becomes momentum, so nothing brakes it.
                momentum = horizontal;
                walk = Vector3.zero;
                if (Time.time >= nextSlideBoost)
                {
                    momentum += horizontal.normalized * slideBoost;
                    nextSlideBoost = Time.time + slideBoostCooldown;
                }
            }
            else if (IsSliding && (!canSlide || momentum.magnitude < slideStopSpeed))
                IsSliding = false;
            if (IsSliding) walk = Vector3.zero;
        }

        void UpdateWallRun()
        {
            if (IsWallRunning)
            {
                bool forward = moveInput.y > 0.1f;
                if (IsGrounded || !forward || Time.time > wallRunEnds || !FindWall(out var normal)) EndWallRun();
                else WallNormal = normal;
                return;
            }

            if (IsGrounded || WorkingLegs < 2 || Time.time < nextWallRun || moveInput.y <= 0.1f) return;
            if (HorizontalSpeed < wallRunMinSpeed || body.linearVelocity.y > 8f) return;
            if (!FindWall(out var wall) || Vector3.Dot(wall, lastWall) > 0.9f) return;
            IsWallRunning = true;
            WallNormal = wall;
            wallRunEnds = Time.time + wallRunTime;
            // A little lift as you hit the wall, so the run arcs before it sinks.
            var v = body.linearVelocity;
            body.linearVelocity = new Vector3(v.x, Mathf.Max(v.y, wallRunLift), v.z);
        }

        // A wall close to the robot's left or right side.
        bool FindWall(out Vector3 normal)
        {
            normal = default;
            var bounds = robot.LocalBounds;
            var center = transform.TransformPoint(bounds.center);
            float reach = bounds.extents.x + wallReach;
            foreach (var side in new[] { transform.right, -transform.right })
            {
                if (!Physics.Raycast(center, side, out var hit, reach, robot.shotMask, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.normal.y) > 0.3f) continue;
                normal = hit.normal;
                return true;
            }
            return false;
        }

        void WallRun()
        {
            var v = body.linearVelocity;
            var along = Vector3.ProjectOnPlane(Flat(v), WallNormal);
            if (along.magnitude < wallRunSpeed) along = along.normalized * wallRunSpeed;
            // Hold on to the wall, and sink down it only slowly.
            momentum = along;
            body.linearVelocity = new Vector3(along.x, Mathf.Max(v.y, -wallRunSink), along.z) - WallNormal * 2f;
        }

        // Walking speed and feel from the Legs and Treads that touch the ground, wherever they are.
        void WalkProfile(out float speed, out float acceleration, out float braking, out bool limping)
        {
            int legs = 0, treads = 0;
            float power = 0f;
            foreach (var part in robot.Parts)
            {
                if (part is not (Leg or Treads) || !part.IsWorking) continue;
                if (part is Leg) legs++;
                else treads++;
                power += part.ModBonus(ModType.Power);
            }
            // Power mods make you faster by their average across everything you walk on.
            float boost = legs + treads > 0 ? 1f + power / (legs + treads) : 1f;
            WorkingLegs = legs;
            HasTreads = treads > 0;
            limping = false;

            if (treads > 0)
            {
                // Legs added on top of Treads make them faster still.
                speed = Mathf.Min(treadSpeed + Stacking.Total(extraLegSpeed, legs, 0.8f, maxWalkSpeed), maxWalkSpeed);
                acceleration = treadAcceleration;
                braking = treadBraking;
                WalkMode = legs > 0 ? $"Treads + {legs} Leg{(legs > 1 ? "s" : "")}" : "Treads";
            }
            else if (legs >= 2)
            {
                speed = Mathf.Min(legSpeed + Stacking.Total(extraLegSpeed, legs - 2, 0.8f, maxWalkSpeed), maxWalkSpeed);
                acceleration = braking = legAcceleration;
                WalkMode = $"{legs} Legs";
            }
            else if (legs == 1)
            {
                speed = limpSpeed;
                acceleration = braking = legAcceleration;
                limping = true;
                WalkMode = "1 Leg (limping)";
            }
            else
            {
                speed = shuffleSpeed;
                acceleration = braking = legAcceleration;
                WalkMode = "No Legs on the ground (shuffling)";
            }
            speed *= boost;
            if (IsSliding) WalkMode += ", sliding";
            if (IsWallRunning) WalkMode += ", wall-running";
        }

        void ApplyGravity()
        {
            var v = body.linearVelocity;
            float g = gravity * (v.y < 0f ? fallGravityMultiplier : 1f);
            if (IsWallRunning) g *= wallRunGravity;
            v.y = Mathf.Max(v.y - g * Time.fixedDeltaTime, -maxFallSpeed);
            body.linearVelocity = v;
        }

        void ApplyPushes()
        {
            if (jumpHeight > 0f)
            {
                var v = body.linearVelocity;
                v.y = Mathf.Max(v.y, JumpSpeed(jumpHeight));
                body.linearVelocity = v;
                landedAt = -1f;
                IsSliding = false;
            }
            if (springPush != Vector3.zero)
            {
                var v = body.linearVelocity;
                // A kick off a wall replaces any fall, so wall-jumps always go up.
                if (springPush.y > 0f && v.y < 0f) v.y = 0f;
                body.linearVelocity = v;
                Push(springPush);
                EndWallRun();
            }
            if (thrust != Vector3.zero) Push(thrust * Time.fixedDeltaTime);
            jumpHeight = 0f;
            springPush = Vector3.zero;
            thrust = Vector3.zero;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        // Standing on anything (the floor, a ledge, a dummy) counts as ground.
        void OnCollisionEnter(Collision collision) => CheckGround(collision);
        void OnCollisionStay(Collision collision) => CheckGround(collision);

        void CheckGround(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                var normal = collision.GetContact(i).normal;
                if (normal.y <= 0.6f) continue;
                touchingGround = true;
                contactNormal = normal;
                return;
            }
        }
    }
}
