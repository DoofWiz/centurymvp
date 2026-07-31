using Century.Battle.Model;
using Century.Core.World;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The signum in the world: a pole, a crossbar and a hanging red cloth, built from code like
    /// the rest of the battle's props. It follows whoever carries it, stands planted when the
    /// bearer holds steady, and lies where it fell — never parented to another transform, because
    /// soldier transforms are rewritten every frame; it reads the sim's authoritative position
    /// each LateUpdate and eases toward it.
    /// </summary>
    public sealed class SignumView : MonoBehaviour
    {
        private const float CarryLean = 8f;
        private const float FallenPitch = 78f;

        private BattleState _state;
        private Transform _rig;

        public void Initialise(BattleState state)
        {
            _state = state;
            Build();
            gameObject.SetActive(state.Signum != SignumStatus.Absent);
        }

        private void Build()
        {
            _rig = new GameObject("SignumRig").transform;
            _rig.SetParent(transform, false);

            var brass = new Color(0.72f, 0.55f, 0.22f);
            var wood = new Color(0.32f, 0.24f, 0.16f);
            var cloth = new Color(0.55f, 0.12f, 0.10f);

            AddBox("Pole", new Vector3(0.045f, 2.5f, 0.045f), new Vector3(0f, 1.25f, 0f), wood);
            AddBox("Crossbar", new Vector3(0.6f, 0.04f, 0.04f), new Vector3(0f, 2.28f, 0f), wood);
            AddBox("Finial", new Vector3(0.12f, 0.16f, 0.05f), new Vector3(0f, 2.52f, 0f), brass);

            // The cloth hangs from the crossbar; a hair of thickness so it lights from both sides.
            AddBox("Cloth", new Vector3(0.5f, 0.62f, 0.015f), new Vector3(0f, 1.94f, 0f), cloth);
            AddBox("Fringe", new Vector3(0.5f, 0.05f, 0.02f), new Vector3(0f, 1.6f, 0f), brass);
        }

        private void AddBox(string name, Vector3 size, Vector3 localPosition, Color colour)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(_rig, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = FxMaterials.Lit(colour);
        }

        private void LateUpdate()
        {
            if (_state == null) return;

            bool visible = _state.Signum != SignumStatus.Absent && _state.Signum != SignumStatus.Lost;
            if (_rig.gameObject.activeSelf != visible) _rig.gameObject.SetActive(visible);
            if (!visible) return;

            Vector3 basePoint = _state.SignumPosition;
            Quaternion target;

            switch (_state.Signum)
            {
                case SignumStatus.Fallen:
                    // Lying where its bearer went down, point toward the ground.
                    basePoint.y = BattleTerrainBuilder.GroundHeight(basePoint) + 0.12f;
                    target = Quaternion.Euler(FallenPitch, transform.rotation.eulerAngles.y, 0f);
                    break;

                default:
                    // Carried at the bearer's shoulder — planted stands dead upright.
                    basePoint += new Vector3(0.25f, 0f, -0.1f);
                    basePoint.y = Mathf.Max(basePoint.y, BattleTerrainBuilder.GroundHeight(basePoint));
                    float lean = _state.SignumPlanted ? 0f : CarryLean;
                    target = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, lean);
                    break;
            }

            float ease = 1f - Mathf.Exp(-10f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, basePoint, ease);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, ease);
        }
    }
}
