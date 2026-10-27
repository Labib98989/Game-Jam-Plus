using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // Fires your actual fist: it flies out, punches the first thing it reaches, and waits there.
    // Reloading (which starts by itself once it's fired) calls it back, and it smacks anything on
    // the way home. Its cell stays reserved on the robot while it's away.
    public class RocketFist : Weapon
    {
        enum State { Docked, Out, Waiting, Returning }

        struct Piece
        {
            public Transform transform;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        State state;
        Transform fist;
        Vector3 velocity;
        float travelled;
        float returnAt;
        readonly List<Piece> pieces = new();
        readonly HashSet<IDamageable> smacked = new();

        RocketFistDefinition Tuning => (RocketFistDefinition)Definition;
        Vector3 Socket => transform.TransformPoint(Definition.nozzleCube);
        public override bool IsReloading => state != State.Docked;

        protected override void Fire()
        {
            var direction = AimFrom(Nozzle);
            fist = new GameObject("Rocket Fist").transform;
            fist.SetPositionAndRotation(Socket, Quaternion.LookRotation(direction));

            // The fist is the cube at the working end, plus its mark.
            pieces.Clear();
            foreach (Transform child in transform)
                if ((child.localPosition - (Vector3)Definition.nozzleCube).sqrMagnitude < 0.5f)
                    pieces.Add(new Piece { transform = child, localPosition = child.localPosition, localRotation = child.localRotation, localScale = child.localScale });
            foreach (var piece in pieces)
            {
                piece.transform.SetParent(fist, true);
                if (piece.transform.TryGetComponent<Collider>(out var collider)) collider.enabled = false;
            }

            velocity = direction * Tuning.fistSpeed;
            travelled = 0f;
            state = State.Out;
            smacked.Clear();
            KickBack(direction, Tuning.kick);
        }

        // Calls the fist back. It's already on its way once it has landed.
        public override void Reload() { }

        protected override void Update()
        {
            base.Update();
            switch (state)
            {
                case State.Out: FlyOut(); break;
                case State.Waiting:
                    if (Time.time >= returnAt) state = State.Returning;
                    break;
                case State.Returning: FlyBack(); break;
            }
        }

        void FlyOut()
        {
            var step = velocity * Time.deltaTime;
            if (Physics.SphereCast(fist.position, 0.6f, velocity.normalized, out var hit, step.magnitude, Robot.shotMask, QueryTriggerInteraction.Ignore))
            {
                fist.position = hit.point - velocity.normalized * 0.5f;
                Damage(hit, ShotDamage, velocity.normalized, Tuning.punchForce);
                Land();
                return;
            }
            fist.position += step;
            travelled += step.magnitude;
            if (travelled >= Tuning.range) Land();
        }

        void Land()
        {
            state = State.Waiting;
            returnAt = Time.time + Tuning.hangTime;
        }

        void FlyBack()
        {
            var toSocket = Socket - fist.position;
            foreach (var collider in Physics.OverlapSphere(fist.position, 0.8f, Robot.shotMask, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target != null && smacked.Add(target)) target.TakeHit(Tuning.returnDamage * Boost(ModType.Damage), toSocket.normalized);
            }
            float step = Tuning.fistSpeed * Time.deltaTime;
            if (toSocket.magnitude <= step)
            {
                Dock();
                return;
            }
            fist.position += toSocket.normalized * step;
            fist.rotation = Quaternion.LookRotation(-toSocket);
        }

        void Dock()
        {
            foreach (var piece in pieces)
            {
                piece.transform.SetParent(transform, false);
                piece.transform.SetLocalPositionAndRotation(piece.localPosition, piece.localRotation);
                piece.transform.localScale = piece.localScale;
                if (piece.transform.TryGetComponent<Collider>(out var collider)) collider.enabled = true;
            }
            pieces.Clear();
            Destroy(fist.gameObject);
            state = State.Docked;
            ShotsLeft = Gun.shotsPerReload;
        }

        void OnDestroy()
        {
            if (fist) Destroy(fist.gameObject);
        }
    }
}
