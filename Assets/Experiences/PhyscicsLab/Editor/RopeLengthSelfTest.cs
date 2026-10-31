using System;
using System.Text;
using UnityEngine;
using PhysicsLab.Lab.Rope;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PhysicsLab.Lab.EditorTools
{
    /// <summary>
    /// Checks that a string never gets longer than its length, even when a hand pulls on it.
    /// It runs a small copy of the force table (ring at the post, two hanging masses, spring scale, free end)
    /// with the same rope maths as the scene. Menu: Lab > Run string length tests. No scene needed.
    /// </summary>
    public static class RopeLengthSelfTest
    {
        private const float Dt = 0.01f;
        private const int SolverIterations = 12;
        private const int LengthFixPasses = 8;
        private const float MaxOvershootMetres = 0.002f;
        private const float MaxSagMetres = 0.02f;     // the ring rests on the post, so a mass may sink a little
        private const float PostRadiusMetres = 0.01f;
        private const float DragGain = 15f;           // same as DesktopPointer
        private const float MaxDragSpeed = 3f;        // same as DesktopPointer
        private const int RestSteps = 2000;           // 20 s
        private const int HandStartStep = 100;

        private const int Ring = 0, Mass1 = 1, Mass2 = 2, Scale = 3, FreeEnd = 4;

        private static readonly Vector3 PlaneAxes = new Vector3(1f, 0f, 1f);
        private static readonly Vector3[] RingKnots =
            { new Vector3(0.04f, 0f, 0f), new Vector3(0f, 0f, 0.04f), new Vector3(-0.04f, 0f, 0f), new Vector3(0f, 0f, -0.04f) };
        private static readonly Vector3 ScaleHook = new Vector3(0.1f, 0f, 0f);

        private static int _failures;
        private static readonly StringBuilder Report = new StringBuilder();

        private sealed class SimBody
        {
            public Vector3 position, velocity, axes;
            public float mass, damping;
            public bool gravityOn;
        }

        private sealed class Outcome
        {
            public float worstOvershoot;   // longest any string got beyond its length, in metres
            public float ringFarthest;     // farthest the ring got from the post, in metres
            public float mass1Rise;        // how far mass 1 ended above where it started
        }

#if UNITY_EDITOR
        [MenuItem("Lab/Run string length tests")]
        private static void RunFromMenu()
        {
            string text = RunAll();
            if (_failures == 0) Debug.Log(text); else Debug.LogError(text);
        }
#endif

        public static int FailureCount => _failures;

        public static string RunAll()
        {
            _failures = 0;
            Report.Length = 0;

            Check("Rest: two masses pull the ring onto the post", Simulate(null, null), -MaxSagMetres);
            Check("A hand pulls the spring scale 1 m away", Simulate(DragBody(Scale, new Vector3(-1.2f, 0.912f, 0.8f)), null), -MaxSagMetres);
            Check("A hand pulls mass 1 sideways", Simulate(DragBody(Mass1, new Vector3(1.5f, 0.48f, 0.8f)), null), -MaxSagMetres);
            Check("A hand pulls mass 1 straight down", Simulate(DragBody(Mass1, new Vector3(0.48f, -0.5f, 0.8f)), null), -MaxSagMetres);
            Check("Pulley 1 pulled out 0.15 m: mass 1 must rise", Simulate(null, PulleyPulledOut(0.15f)), 0.10f);

            Report.AppendLine(_failures == 0 ? "ALL STRING TESTS PASSED" : $"{_failures} STRING TEST(S) FAILED");
            return Report.ToString();
        }

        private static void Check(string name, Outcome outcome, float minimumMass1Rise)  // a negative value allows a sag
        {
            bool lengthOk = outcome.worstOvershoot <= MaxOvershootMetres;
            bool ringOk = outcome.ringFarthest <= PostRadiusMetres + 0.0005f;
            bool riseOk = outcome.mass1Rise >= minimumMass1Rise;
            bool ok = lengthOk && ringOk && riseOk;
            if (!ok) _failures++;
            Report.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}: string {outcome.worstOvershoot * 1000f:F1} mm too long at worst " +
                              $"(limit {MaxOvershootMetres * 1000f:F0}), ring {outcome.ringFarthest * 100f:F1} cm from the post, mass 1 rose {outcome.mass1Rise * 100f:F1} cm");
        }

        // ---------- the small force table ----------

        private static Outcome Simulate(Action<SimBody[], int> hand, Func<int, Vector3> guide1Offset)
        {
            SimBody[] bodies = BuildBodies();
            Vector3 post = new Vector3(0f, 0.905f, 0.8f);
            Vector3[] guides = { new Vector3(0.48f, 0.93f, 0.8f), new Vector3(0f, 0.93f, 1.28f) };
            RopeLink[] links = BuildLinks(guides);

            var outcome = new Outcome();
            float startY = bodies[Mass1].position.y;
            for (int step = 0; step < RestSteps; step++)
            {
                if (hand != null && step >= HandStartStep) hand(bodies, step);
                if (guide1Offset != null) links[0].guides[0] = guides[0] + guide1Offset(step);

                HoldRingNearPost(bodies[Ring], post);
                SolveRopes(bodies, links, post);
                Integrate(bodies);

                outcome.worstOvershoot = Mathf.Max(outcome.worstOvershoot, WorstOvershoot(bodies, links));
                Vector3 fromPost = bodies[Ring].position - post;
                fromPost.y = 0f;
                outcome.ringFarthest = Mathf.Max(outcome.ringFarthest, fromPost.magnitude);
            }
            outcome.mass1Rise = bodies[Mass1].position.y - startY;
            return outcome;
        }

        private static SimBody[] BuildBodies()
        {
            return new[]
            {
                Body(new Vector3(0f, 0.907f, 0.8f), 0.2f, PlaneAxes, true, 6f),            // ring
                Body(new Vector3(0.48f, 0.48f, 0.8f), 0.1f, Vector3.one, true, 0.3f),      // mass 1
                Body(new Vector3(0f, 0.48f, 1.28f), 0.1f, Vector3.one, true, 0.3f),        // mass 2
                Body(new Vector3(-0.25f, 0.912f, 0.8f), 0.5f, PlaneAxes, false, 6f),       // spring scale
                Body(new Vector3(0f, 0.907f, 0.5f), 0.001f, PlaneAxes, false, 20f)         // free end
            };
        }

        private static SimBody Body(Vector3 position, float mass, Vector3 axes, bool gravityOn, float damping)
        {
            return new SimBody { position = position, mass = mass, axes = axes, gravityOn = gravityOn, damping = damping };
        }

        private static RopeLink[] BuildLinks(Vector3[] guides)
        {
            return new[]
            {
                new RopeLink { bodyA = Ring, bodyB = Mass1, guides = new[] { guides[0] }, length = 0.8906f, offsetA = RingKnots[0] },
                new RopeLink { bodyA = Ring, bodyB = Mass2, guides = new[] { guides[1] }, length = 0.8906f, offsetA = RingKnots[1] },
                new RopeLink { bodyA = Ring, bodyB = Scale, length = 0.45f, offsetA = RingKnots[2], offsetB = ScaleHook },
                new RopeLink { bodyA = Ring, bodyB = FreeEnd, length = 0.5f, offsetA = RingKnots[3] }
            };
        }

        // What TableBounds does on the ring: put it back inside the circle and drop the speed that pushed it out.
        private static void HoldRingNearPost(SimBody ring, Vector3 post)
        {
            Vector3 offset = ring.position - post;
            offset.y = 0f;
            if (offset.magnitude <= PostRadiusMetres) return;

            Vector3 outward = offset.normalized;
            Vector3 clamped = post + outward * PostRadiusMetres;
            ring.position = new Vector3(clamped.x, ring.position.y, clamped.z);
            ring.velocity -= outward * Vector3.Dot(ring.velocity, outward);
        }

        // What RopeNetwork does each physics step.
        private static void SolveRopes(SimBody[] bodies, RopeLink[] links, Vector3 post)
        {
            var states = new RopeBodyState[bodies.Length];
            for (int i = 0; i < bodies.Length; i++) states[i] = ToState(bodies[i], i == Ring, post);

            foreach (RopeLink link in links)
            {
                link.pointA = bodies[link.bodyA].position + link.offsetA;
                link.pointB = bodies[link.bodyB].position + link.offsetB;
            }

            RopeSolver.Solve(states, links, links.Length, Dt, SolverIterations, true);
            RopeSolver.ProjectLengths(states, links, links.Length, LengthFixPasses);

            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].velocity = Vector3.Scale(states[i].velocity, bodies[i].axes);
                bodies[i].position = states[i].position;
            }
        }

        private static RopeBodyState ToState(SimBody body, bool limited, Vector3 post)
        {
            var state = new RopeBodyState
            {
                freeAxes = body.axes,
                inverseMass = 1f / body.mass,
                velocity = Vector3.Scale(body.velocity, body.axes),
                position = body.position,
                gravity = body.gravityOn ? Vector3.Scale(new Vector3(0f, -9.81f, 0f), body.axes) : Vector3.zero
            };
            if (limited)
            {
                state.hasLimit = true;
                state.limitCenter = post;
                state.limitRadius = PostRadiusMetres;
            }
            return state;
        }

        // What PhysX does: gravity, damping, then move.
        private static void Integrate(SimBody[] bodies)
        {
            foreach (SimBody body in bodies)
            {
                if (body.gravityOn) body.velocity += Vector3.Scale(new Vector3(0f, -9.81f * Dt, 0f), body.axes);
                body.velocity = Vector3.Scale(body.velocity * (1f / (1f + body.damping * Dt)), body.axes);
                body.position += body.velocity * Dt;
            }
        }

        private static float WorstOvershoot(SimBody[] bodies, RopeLink[] links)
        {
            float worst = 0f;
            foreach (RopeLink link in links)
            {
                link.pointA = bodies[link.bodyA].position + link.offsetA;
                link.pointB = bodies[link.bodyB].position + link.offsetB;
                worst = Mathf.Max(worst, RopeSolver.PathLength(link) - link.length);
            }
            return worst;
        }

        // ---------- hands ----------

        // Same chase as DesktopPointer: the body speed goes toward the aim point, capped.
        private static Action<SimBody[], int> DragBody(int index, Vector3 target)
        {
            return (bodies, step) =>
            {
                Vector3 velocity = (target - bodies[index].position) * DragGain;
                bodies[index].velocity = Vector3.ClampMagnitude(velocity, MaxDragSpeed);
            };
        }

        private static Func<int, Vector3> PulleyPulledOut(float metres)
        {
            return step => new Vector3(Mathf.Min(metres, Mathf.Max(0, step - HandStartStep) * 0.0015f), 0f, 0f);
        }
    }
}
