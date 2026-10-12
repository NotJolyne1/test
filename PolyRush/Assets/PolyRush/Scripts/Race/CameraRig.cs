using UnityEngine;

namespace PolyRush
{
    /// <summary>
    /// Chase / far chase / hood cameras for racing and a slow cinematic fly-along for the menus.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public enum Mode { Menu, Race }

        public Mode CurrentMode = Mode.Menu;
        public CarController Target;
        public TrackPath MenuPath;

        Camera cam;
        Vector3 smoothDir = Vector3.forward;
        float fov = 62f;
        float menuDistance;
        float menuAngle;
        Vector3 menuFocus;
        float shake;
        int layerMask;

        void Awake()
        {
            cam = GetComponent<Camera>();
            layerMask = ~((1 << 2) | (1 << 1));
        }

        public void SnapBehindTarget()
        {
            if (Target == null) return;
            smoothDir = Target.transform.forward;
            fov = BaseFov();
            LateUpdate();
        }

        public void AddShake(float amount) { shake = Mathf.Max(shake, amount); }

        float BaseFov() { return GameSettings.CameraMode == 2 ? 72f : 64f; }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (CurrentMode == Mode.Menu) UpdateMenu(dt);
            else if (Target != null) UpdateRace(Time.timeScale > 0.01f ? dt : 0f);
        }

        void UpdateMenu(float dt)
        {
            if (MenuPath == null) return;
            menuDistance += dt * 16f;
            if (menuDistance > MenuPath.TotalLength) menuDistance = 0f;
            menuAngle += dt * 7f;
            int i = MenuPath.IndexAtDistance(menuDistance);
            Vector3 focus = MenuPath.Pos[i] + Vector3.up * 2f;
            menuFocus = Vector3.Lerp(menuFocus == Vector3.zero ? focus : menuFocus, focus, 1f - Mathf.Exp(-dt * 2f));
            Vector3 offset = Quaternion.Euler(0f, menuAngle, 0f) * new Vector3(0f, 0f, -48f);
            Vector3 pos = menuFocus + offset + Vector3.up * 24f;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(menuFocus - pos, Vector3.up);
            cam.fieldOfView = 55f;
        }

        void UpdateRace(float dt)
        {
            Transform t = Target.transform;
            Vector3 vel = Target.Body != null ? Target.Body.velocity : Vector3.zero;
            float speed = vel.magnitude;
            int mode = GameSettings.CameraMode;

            if (mode == 2)
            {
                // hood camera
                Vector3 p = t.TransformPoint(new Vector3(0f, 1.32f, 0.35f));
                transform.position = p;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(t.forward, t.up), dt > 0 ? 1f - Mathf.Exp(-dt * 18f) : 1f);
            }
            else
            {
                float dist = mode == 1 ? 9.5f : 6.6f;
                float height = mode == 1 ? 3.4f : 2.3f;

                // blend car heading with travel direction so slides and jumps look natural
                Vector3 dir = t.forward;
                if (speed > 6f) dir = Vector3.Slerp(t.forward, vel / speed, 0.35f);
                dir.y *= 0.5f;
                if (dir.sqrMagnitude < 0.001f) dir = t.forward;
                dir.Normalize();
                if (dt > 0f) smoothDir = Vector3.Slerp(smoothDir, dir, 1f - Mathf.Exp(-dt * 6f)).normalized;

                float extra = Mathf.Clamp01(speed / 80f) * 0.8f;
                Vector3 pivot = t.position + Vector3.up * 1.2f;
                Vector3 desired = pivot - smoothDir * (dist + extra) + Vector3.up * height;

                // keep the camera out of walls / terrain
                RaycastHit hit;
                Vector3 toCam = desired - pivot;
                if (Physics.SphereCast(pivot, 0.35f, toCam.normalized, out hit, toCam.magnitude, layerMask, QueryTriggerInteraction.Ignore))
                    desired = pivot + toCam.normalized * Mathf.Max(1.5f, hit.distance - 0.1f);

                transform.position = desired;
                Vector3 look = pivot + smoothDir * 4f;
                transform.rotation = Quaternion.LookRotation(look - desired, Vector3.up);
            }

            // speed FOV + boost kick
            float target = BaseFov() + Mathf.Clamp01(speed / 75f) * 14f + Target.BoostVisual * 6f;
            if (dt > 0f) fov = Mathf.Lerp(fov, target, 1f - Mathf.Exp(-dt * 4f));
            cam.fieldOfView = fov;

            if (shake > 0.001f && dt > 0f)
            {
                transform.position += Random.insideUnitSphere * shake * 0.25f;
                shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            }
        }
    }
}
