using UnityEngine;

namespace CoreDump
{
    // A gun. It fires the way it points: at the crosshair when it faces forward, otherwise at the
    // nearest enemy in that direction. Every shot kicks the robot the other way. It fires its shots,
    // then reloads by itself; R reloads every gun at once. Reloading shows on the gun: it goes dark
    // and pulses until it's ready.
    public abstract class Weapon : PartModule
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        float reloadEnds;
        float nextShot;
        bool tinted;
        Renderer[] renderers;
        MaterialPropertyBlock tint;

        protected WeaponDefinition Gun => (WeaponDefinition)Definition;
        // Damage per shot, with any Damage mods touching the gun.
        protected float ShotDamage => Gun.damage * Boost(ModType.Damage);
        public int ShotsLeft { get; protected set; }
        public virtual bool IsReloading => reloadEnds > 0f;

        protected override void OnFitted()
        {
            ShotsLeft = Gun.shotsPerReload;
            renderers = GetComponentsInChildren<Renderer>();
            tint = new MaterialPropertyBlock();
        }

        public override void OnButtonDown() => TryFire();

        public override void OnButtonHeld()
        {
            if (Gun.automatic) TryFire();
        }

        protected bool TryFire()
        {
            if (IsReloading || Time.time < nextShot || ShotsLeft <= 0) return false;
            nextShot = Time.time + Gun.fireInterval / Boost(ModType.FireRate);
            ShotsLeft--;
            Fire();
            Juice.Play(Gun.fireSound);
            Juice.Flash(Nozzle, 0.5f);
            Juice.Shake(Gun.kick * 0.012f);
            if (ShotsLeft <= 0) Reload();
            return true;
        }

        protected abstract void Fire();

        // Starts reloading, unless the gun is full or already reloading.
        public virtual void Reload()
        {
            if (IsReloading || ShotsLeft >= Gun.shotsPerReload) return;
            reloadEnds = Time.time + Gun.reloadTime / Boost(ModType.ReloadSpeed);
        }

        protected virtual void Update()
        {
            if (reloadEnds > 0f && Time.time >= reloadEnds)
            {
                reloadEnds = 0f;
                ShotsLeft = Gun.shotsPerReload;
                Juice.Play(Sound.Reload);
            }
            ShowReload();
        }

        void ShowReload()
        {
            if (renderers == null) return;
            if (IsReloading)
            {
                var color = Definition.material ? Definition.material.GetColor(BaseColor) : Color.gray;
                tint.SetColor(BaseColor, color * (0.35f + 0.15f * Mathf.Sin(Time.time * 14f)));
                foreach (var r in renderers)
                    if (r && r.sharedMaterial == Definition.material) r.SetPropertyBlock(tint);
                tinted = true;
            }
            else if (tinted)
            {
                foreach (var r in renderers)
                    if (r && r.sharedMaterial == Definition.material) r.SetPropertyBlock(null);
                tinted = false;
            }
        }

        // Where to shoot from `muzzle`: at the crosshair when the gun faces forward; otherwise at the
        // nearest enemy within a cone of where it points; otherwise straight out.
        protected Vector3 AimFrom(Vector3 muzzle)
        {
            var direction = Direction;
            if (Vector3.Angle(direction, Robot.transform.forward) <= Gun.forwardCone)
                return (Robot.AimPoint - muzzle).normalized;
            var target = Targeting.Nearest(muzzle, direction, Gun.autoAimCone, Gun.range);
            return target ? (target.AimPoint - muzzle).normalized : direction;
        }

        protected void KickBack(Vector3 shotDirection, float amount) => Robot.Motor.Kick(-shotDirection * amount * Boost(ModType.Recoil));

        // A shot that counts anything passing close to it as a hit.
        protected bool Hitscan(Vector3 from, Vector3 direction, float range, out RaycastHit hit) =>
            Physics.SphereCast(from, 0.25f, direction, out hit, range, Robot.shotMask, QueryTriggerInteraction.Ignore);

        protected static void Damage(RaycastHit hit, float damage, Vector3 direction, float knockback)
        {
            hit.collider.GetComponentInParent<IDamageable>()?.TakeHit(damage, direction);
            if (hit.rigidbody && !hit.rigidbody.isKinematic)
                hit.rigidbody.AddForceAtPosition(direction * knockback, hit.point, ForceMode.Impulse);
        }
    }
}
