using UnityEngine;

namespace TowerDefense
{
    public class CustomVector3
    {
        public float x;
        public float y;
        public float z;

        public CustomVector3() { }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }

        public CustomVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public CustomVector3(Vector3 vector)
        {
            x = vector.x;
            y = vector.y;
            z = vector.z;
        }
    }

    public static class Ext
    {
        public static CustomVector3 ToCustomVector3(this Vector3 vector)
        {
            return new(vector);
        }
    }
}
