using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    [CreateAssetMenu(menuName = "MonsterTactics/Art/Sprite Visual Bounds Database")]
    public sealed class MonsterTacticsSpriteVisualBoundsDatabase : ScriptableObject
    {
        [Serializable]
        public struct SpriteBounds
        {
            public Sprite Sprite;
            // UV-normalized opaque pixel region in the sprite's own rectangle.
            public Rect VisibleRect;
        }

        public List<SpriteBounds> Entries = new List<SpriteBounds>();
    }

}
