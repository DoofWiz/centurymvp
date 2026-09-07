using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// A walk-on part for the opening's cinematic: a legionary built from the same rig and gear as
    /// everyone else, but driven by the director rather than the simulation. He walks where he is
    /// sent, faces what he is told to, and never fights.
    /// </summary>
    public sealed class OpeningExtra : MonoBehaviour
    {
        private SoldierRig _rig;
        private CombatantGear _gear;
        private BattleCombatant _dummy;
        private Vector3? _target;
        private float _speed = 2.6f;
        private float _currentSpeed;

        /// <summary>True when no walk is in progress.</summary>
        public bool Arrived => !_target.HasValue;

        public static OpeningExtra Spawn(Transform parent, Vector3 groundPosition, Vector3 facing, bool roman, int variant = 17)
        {
            var extra = new GameObject(roman ? "Legionary (extra)" : "Warrior (extra)").AddComponent<OpeningExtra>();
            extra.transform.SetParent(parent, false);
            extra.transform.position = groundPosition;
            extra.Face(facing);

            extra._dummy = new BattleCombatant
            {
                DisplayName = "Extra",
                Weapon = roman ? WeaponClass.Sword : WeaponClass.Spear,
                HasShield = true,
                IsPlayerSide = roman
            };

            extra._rig = SoldierRig.Build(extra.transform, 0.95f, roman, OfficerRole.None, false, variant);
            extra._gear = new CombatantGear(extra.transform, extra._dummy.Weapon, hasShield: true);
            return extra;
        }

        public void WalkTo(Vector3 groundTarget, float speed)
        {
            _target = groundTarget;
            _speed = Mathf.Max(0.1f, speed);
        }

        public void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_target.HasValue)
            {
                Vector3 to = _target.Value - transform.position;
                to.y = 0f;

                if (to.magnitude <= 0.35f)
                {
                    _target = null;
                    _currentSpeed = 0f;
                }
                else
                {
                    Vector3 dir = to.normalized;
                    Vector3 next = transform.position + dir * (_speed * dt);
                    transform.position = BattleTerrainBuilder.Grounded(next);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 540f * dt);
                    _currentSpeed = _speed;
                }
            }
            else
            {
                _currentSpeed = 0f;
            }

            _dummy.WorldPosition = transform.position;
            _dummy.Facing = transform.forward;

            _rig?.Animate(_currentSpeed, dt);
            _gear?.Pose(_dummy, dt, _rig);
        }
    }
}
