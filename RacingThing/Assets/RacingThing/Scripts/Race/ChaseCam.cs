using UnityEngine;

namespace RacingThing
{
    [RequireComponent(typeof(Camera))]
    public class ChaseCam : MonoBehaviour
    {
        public bool Menu = true;
        public CarController Target;
        public TrackPath MenuPath;

        Camera cam;
        Vector3 dir = Vector3.forward;
        Vector3 camUp = Vector3.up;
        float fov = 64f;
        float shake;
        float menuDist, menuAngle;
        Vector3 menuFocus;
        int mask;

        void Awake()
        {
            cam = GetComponent<Camera>();
            mask = ~((1 << 2) | (1 << 1));
        }

        public void Snap()
        {
            if (Target == null) return;
            dir = Target.transform.forward;
            camUp = Target.transform.up;
            fov = BaseFov;
            LateUpdate();
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        float BaseFov { get { return Settings.CameraMode == 2 ? 74f : 66f; } }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (Menu) MenuShot(dt);
            else if (Target != null) Follow(Time.timeScale > 0.01f ? dt : 0f);
        }

        void MenuShot(float dt)
        {
            if (MenuPath == null) return;
            menuDist = (menuDist + dt * 14f) % MenuPath.TotalLength;
            menuAngle += dt * 6f;
            var focus = MenuPath.Pos[MenuPath.IndexAtDistance(menuDist)] + Vector3.up * 2f;
            menuFocus = menuFocus == Vector3.zero ? focus : Vector3.Lerp(menuFocus, focus, 1f - Mathf.Exp(-dt * 1.5f));
            var pos = menuFocus + Quaternion.Euler(0f, menuAngle, 0f) * new Vector3(0f, 0f, -42f) + Vector3.up * 20f;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(menuFocus - pos);
            cam.fieldOfView = 55f;
        }

        void Follow(float dt)
        {
            Transform t = Target.transform;
            Vector3 vel = Target.Body.velocity;
            float speed = vel.magnitude;

            // follow the car's own "up" while it's on the road so loops look right,
            // drift back to world up in the air
            Vector3 wantUp = Target.Grounded ? t.up : Vector3.up;
            if (dt > 0f) camUp = Vector3.Slerp(camUp, wantUp, 1f - Mathf.Exp(-dt * (Target.Grounded ? 8f : 2f))).normalized;

            if (Settings.CameraMode == 2)
            {
                transform.position = t.TransformPoint(new Vector3(0f, 0.75f, 2.3f));
                var want = Quaternion.LookRotation(t.forward, t.up);
                transform.rotation = dt > 0f ? Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-dt * 20f)) : want;
            }
            else
            {
                bool far = Settings.CameraMode == 1;
                float back = far ? 8.5f : 6f, height = far ? 2.9f : 2f;

                Vector3 d = t.forward;
                if (speed > 6f) d = Vector3.Slerp(t.forward, vel / speed, 0.4f);
                if (dt > 0f) dir = Vector3.Slerp(dir, d, 1f - Mathf.Exp(-dt * 7f)).normalized;

                Vector3 pivot = t.position + camUp * 1.1f;
                Vector3 pos = pivot - dir * (back + Mathf.Clamp01(speed / 90f) * 0.8f) + camUp * height;
                Vector3 toCam = pos - pivot;
                RaycastHit hit;
                if (Physics.SphereCast(pivot, 0.3f, toCam.normalized, out hit, toCam.magnitude, mask, QueryTriggerInteraction.Ignore))
                    pos = pivot + toCam.normalized * Mathf.Max(1.5f, hit.distance - 0.1f);

                transform.position = pos;
                transform.rotation = Quaternion.LookRotation(pivot + dir * 4f - pos, camUp);
            }

            float target = BaseFov + Mathf.Clamp01(speed / 80f) * 16f + Target.Boost * 6f;
            if (dt > 0f) fov = Mathf.Lerp(fov, target, 1f - Mathf.Exp(-dt * 4f));
            cam.fieldOfView = fov;

            if (shake > 0.001f && dt > 0f)
            {
                transform.position += Random.insideUnitSphere * shake * 0.2f;
                shake = Mathf.MoveTowards(shake, 0f, dt * 3f);
            }
        }
    }
}
