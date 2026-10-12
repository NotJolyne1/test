using UnityEngine;

namespace PolyRush
{
    // Tyre smoke when sliding, flames out the back when boosting.
    [RequireComponent(typeof(CarController))]
    public class CarFx : MonoBehaviour
    {
        CarController car;
        ParticleSystem smokeL, smokeR, flameL, flameR;

        void Start()
        {
            car = GetComponent<CarController>();
            smokeL = MakeSmoke(new Vector3(-0.88f, 0.15f, -1.32f));
            smokeR = MakeSmoke(new Vector3(0.88f, 0.15f, -1.32f));
            flameL = MakeFlame(new Vector3(-0.35f, 0.32f, -2.25f));
            flameR = MakeFlame(new Vector3(0.35f, 0.32f, -2.25f));
        }

        ParticleSystem MakeSmoke(Vector3 local)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startColor = new Color(0.93f, 0.93f, 0.95f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            main.gravityModifier = -0.05f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Materials.Particles;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeFlame(Vector3 local)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f, 0.9f), new Color(1f, 0.45f, 0.1f, 0.9f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.05f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Materials.Particles;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        void Update()
        {
            if (car == null || smokeL == null) return;
            float slip = car.Grounded ? Mathf.Clamp01((car.Slip - 5f) / 8f) : 0f;
            if (car.Drifting && car.Grounded) slip = Mathf.Max(slip, 0.7f);
            float rate = slip * 60f;
            var e1 = smokeL.emission; e1.rateOverTime = rate;
            var e2 = smokeR.emission; e2.rateOverTime = rate;
            float flame = car.Boost * 120f;
            var f1 = flameL.emission; f1.rateOverTime = flame;
            var f2 = flameR.emission; f2.rateOverTime = flame;
        }

        public void Clear()
        {
            if (smokeL == null) return;
            smokeL.Clear();
            smokeR.Clear();
            flameL.Clear();
            flameR.Clear();
        }
    }
}
