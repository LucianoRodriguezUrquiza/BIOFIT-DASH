using UnityEngine;

namespace Biofit.Geometry
{
    public sealed class GeometryWorldItem : MonoBehaviour
    {
        public int Id;
        public string Kind;
        public bool Solid => Kind == "ground" || Kind == "block";
        private bool consumed;
        public bool TryConsume()
        {
            if (consumed) return false;
            consumed = true;
            var view = GetComponent<SpriteRenderer>();
            if (view != null) view.color = new Color(.22f,.32f,.4f,.45f);
            return true;
        }
    }
}
