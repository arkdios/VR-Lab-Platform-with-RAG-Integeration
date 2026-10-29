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
    /// Checks the rope maths against hand-calculated physics. Menu: Lab > Run rope tests.
    /// The numbers to expect are in each test's comment. Pure maths, no scene needed.
    /// </summary>
    public static class RopeSolverSelfTest
    {
        private static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);
        private static readonly Vector3 PlaneAxes = new Vector3(1f, 0f, 1f);     // Y frozen, like the ring
        private static readonly Vector3 AllAxes = new Vector3(1f, 1f, 1f);

        private static int _failures;
        private static readonly StringBuilder Report = new StringBuilder();

#if UNITY_EDITOR
        [MenuItem("Lab/Run rope tests")]
        private static void RunFromMenu()
        {
            string text = RunAll();
            if (_failures == 0) Debug.Log(text); else Debug.LogError(text);
        }
#endif

        /// <summary>Runs every test. Returns the report text. Failures are counted in FailureCount.</summary>
        public static string RunAll()
        {
            _failures = 0;
            Report.Length = 0;
            foreach (float dt in new[] { 0.02f, 0.01f })
            {
                Report.AppendLine($"--- time step {dt}");
                AtwoodCase(dt);
                StaticCase(dt);
                SlackToTautImpact(dt);
                ForceTableCase(dt, 0.45f, true);
                ForceTableCase(dt, 0.30f, false);
            }
            ArcLength();
            Report.AppendLine(_failures == 0 ? "ALL TESTS PASSED" : $"{_failures} TEST(S) FAILED");
            return Report.ToString();
        }

        public static int FailureCount => _failures;

        private static RopeBodyState Body(float mass, Vector3 axes, bool gravityOn, Vector3 velocity)
        {
            return new RopeBodyState
            {
                velocity = velocity,
                inverseMass = mass <= 0f ? 0f : 1f / mass,
                freeAxes = axes,
                gravity = gravityOn ? Vector3.Scale(Gravity, axes) : Vector3.zero
            };
        }

        // PhysX order: add gravity to velocity, then move the body.
        private static void Move(RopeBodyState[] bodies, Vector3[] positions, float dt)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].inverseMass <= 0f) continue;
                bodies[i].velocity += bodies[i].gravity * dt;
                positions[i] += bodies[i].velocity * dt;
            }
        }

        // Ring 0.2 kg on a frictionless plane, 0.1 kg hanging over a guide.
        // Expect: acceleration 0.1 * 9.81 / 0.3 = 3.27 m/s^2, tension 0.2 * 3.27 = 0.654 N.
        private static void AtwoodCase(float dt)
        {
            var bodies = new[] { Body(0.2f, PlaneAxes, true, Vector3.zero), Body(0.1f, AllAxes, true, Vector3.zero) };
            var positions = new[] { Vector3.zero, new Vector3(0.4f, -0.3f, 0f) };
            var link = new RopeLink { bodyA = 0, bodyB = 1, guides = new[] { new Vector3(0.4f, 0f, 0f) }, length = 0.7f };

            int steps = Mathf.RoundToInt(0.3f / dt);
            float tensionSum = 0f;
            for (int s = 0; s < steps; s++)
            {
                link.pointA = positions[0];
                link.pointB = positions[1];
                RopeSolver.Solve(bodies, new[] { link }, 1, dt, 12, true);
                tensionSum += link.tension;
                Move(bodies, positions, dt);
            }
            float acceleration = bodies[0].velocity.x / (steps * dt);
            Check($"Atwood acceleration (expect 3.27)", acceleration, 3.27f, 0.02f);
            Check($"Atwood tension (expect 0.654 N)", tensionSum / steps, 0.654f, 0.02f);
        }

        // Ring 0.2 kg, two 0.1 kg masses over guides at 0 and 90 degrees, a third rope to a fixed anchor at 225 degrees.
        // Expect: tensions 0.981, 0.981, 1.387 N and the ring stays put.
        private static void StaticCase(float dt)
        {
            float d = 0.4f;
            float c = d * Mathf.Cos(225f * Mathf.PI / 180f);
            float sn = d * Mathf.Sin(225f * Mathf.PI / 180f);
            var bodies = new[]
            {
                Body(0.2f, PlaneAxes, true, Vector3.zero),
                Body(0.1f, AllAxes, true, Vector3.zero),
                Body(0.1f, AllAxes, true, Vector3.zero),
                Body(0f, AllAxes, false, Vector3.zero)
            };
            var positions = new[]
            {
                Vector3.zero, new Vector3(d, -0.3f, 0f), new Vector3(0f, -0.3f, d), new Vector3(c, 0f, sn)
            };
            var links = new[]
            {
                new RopeLink { bodyA = 0, bodyB = 1, guides = new[] { new Vector3(d, 0f, 0f) }, length = d + 0.3f },
                new RopeLink { bodyA = 0, bodyB = 2, guides = new[] { new Vector3(0f, 0f, d) }, length = d + 0.3f },
                new RopeLink { bodyA = 0, bodyB = 3, length = d }
            };

            int steps = Mathf.RoundToInt(3f / dt);
            float[] tensionAverage = new float[3];
            int counted = 0;
            for (int s = 0; s < steps; s++)
            {
                for (int k = 0; k < 3; k++) { links[k].pointA = positions[0]; links[k].pointB = positions[k + 1]; }
                RopeSolver.Solve(bodies, links, 3, dt, 12, true);
                if (s >= steps - Mathf.RoundToInt(0.5f / dt))
                {
                    for (int k = 0; k < 3; k++) tensionAverage[k] += links[k].tension;
                    counted++;
                }
                Move(bodies, positions, dt);
            }
            Check("Static tension 1 (expect 0.981 N)", tensionAverage[0] / counted, 0.981f, 0.03f);
            Check("Static tension 2 (expect 0.981 N)", tensionAverage[1] / counted, 0.981f, 0.03f);
            Check("Static tension 3 (expect 1.387 N)", tensionAverage[2] / counted, 1.387f, 0.04f);
            Check("Static ring drift in metres (expect 0)", positions[0].magnitude, 0f, 0.002f);
        }

        // A 0.1 kg ball flies out at 5 m/s on a 0.5 m rope whose other end is held. Expect: no NaN, the rope never
        // stretches more than 2 cm (about 1 cm at 0.02 s, 0 at 0.01 s), and the ball speed stays below its launch
        // speed plus what gravity adds.
        private static void SlackToTautImpact(float dt)
        {
            var bodies = new[] { Body(0f, AllAxes, false, Vector3.zero), Body(0.1f, AllAxes, true, new Vector3(5f, 0f, 0f)) };
            var positions = new[] { Vector3.zero, new Vector3(0.1f, 0f, 0f) };
            var link = new RopeLink { bodyA = 0, bodyB = 1, length = 0.5f };

            float maxStretch = 0f;
            float maxSpeed = 0f;
            bool broken = false;
            int steps = Mathf.RoundToInt(2f / dt);
            for (int s = 0; s < steps; s++)
            {
                link.pointA = positions[0];
                link.pointB = positions[1];
                RopeSolver.Solve(bodies, new[] { link }, 1, dt, 12, true);
                Move(bodies, positions, dt);
                float stretch = positions[1].magnitude - 0.5f;
                maxStretch = Mathf.Max(maxStretch, stretch);
                maxSpeed = Mathf.Max(maxSpeed, bodies[1].velocity.magnitude);
                if (float.IsNaN(positions[1].x) || float.IsNaN(bodies[1].velocity.x)) broken = true;
            }
            CheckAtMost("Impact: NaN found (expect 0)", broken ? 1f : 0f, 0f);
            CheckAtMost("Impact: max stretch in metres (expect <= 0.02)", maxStretch, 0.02f);
            CheckAtMost("Impact: max speed (expect <= 5 + 9.81 * 2)", maxSpeed, 5f + 9.81f * 2f);
        }

        // The real lab: ring held by the post (1 cm), 0.1 kg over a pulley at 0 degrees, 0.2 kg over a pulley at
        // 90 degrees, the spring scale held at a fixed place on a 0.45 m arm. Ring and string damping match the scene.
        // Balanced (scale placed opposite the pull): expect the scale to read 2.194 N and the ring to sit at the post.
        // Slack (scale too close): expect 0 N on the scale and the ring pressed 1 cm against the post.
        private static void ForceTableCase(float dt, float scaleDistance, bool balanced)
        {
            const float post = 0.01f;
            float pull1 = 0.1f * 9.81f, pull2 = 0.2f * 9.81f;
            float expected = Mathf.Sqrt(pull1 * pull1 + pull2 * pull2);
            Vector3 away = new Vector3(-pull1, 0f, -pull2) / expected; // opposite the pull of the masses

            var bodies = new[]
            {
                Body(0.2f, PlaneAxes, true, Vector3.zero),     // ring
                Body(0.1f, AllAxes, true, Vector3.zero),       // mass on arm 1
                Body(0.2f, AllAxes, true, Vector3.zero),       // mass on arm 2
                Body(0f, AllAxes, false, Vector3.zero),        // scale, held
                Body(0.001f, PlaneAxes, false, Vector3.zero)   // free end
            };
            var positions = new[]
            {
                Vector3.zero, new Vector3(0.4f, -0.45f, 0f), new Vector3(0f, -0.45f, 0.4f),
                away * scaleDistance, new Vector3(0f, 0f, -0.3f)
            };
            float[] damping = { 6f, 0.3f, 0.3f, 0f, 20f };
            var links = new[]
            {
                new RopeLink { bodyA = 0, bodyB = 1, guides = new[] { new Vector3(0.4f, 0f, 0f) }, length = 0.85f },
                new RopeLink { bodyA = 0, bodyB = 2, guides = new[] { new Vector3(0f, 0f, 0.4f) }, length = 0.85f },
                new RopeLink { bodyA = 0, bodyB = 3, length = 0.45f },
                new RopeLink { bodyA = 0, bodyB = 4, length = 0.5f }
            };

            int steps = Mathf.RoundToInt(6f / dt);
            int lastHalfSecond = Mathf.RoundToInt(0.5f / dt);
            float scaleReading = 0f, ringOffset = 0f;
            for (int s = 0; s < steps; s++)
            {
                for (int k = 0; k < 4; k++) { links[k].pointA = positions[0]; links[k].pointB = positions[k + 1]; }
                RopeSolver.Solve(bodies, links, 4, dt, 12, true);
                for (int i = 0; i < bodies.Length; i++)
                {
                    if (bodies[i].inverseMass <= 0f) continue;
                    bodies[i].velocity += bodies[i].gravity * dt;
                    bodies[i].velocity *= Mathf.Max(0f, 1f - damping[i] * dt); // Rigidbody drag
                    positions[i] += bodies[i].velocity * dt;
                }
                // The post: the ring cannot leave a 1 cm circle (TableBounds). Outward speed is removed.
                Vector3 flat = new Vector3(positions[0].x, 0f, positions[0].z);
                if (flat.magnitude > post)
                {
                    Vector3 outward = flat.normalized;
                    positions[0] = outward * post;
                    bodies[0].velocity -= outward * Mathf.Max(0f, Vector3.Dot(bodies[0].velocity, outward));
                }
                if (s >= steps - lastHalfSecond)
                {
                    scaleReading += links[2].tension / lastHalfSecond;
                    ringOffset = Mathf.Max(ringOffset, positions[0].magnitude);
                }
            }

            if (balanced)
            {
                Check($"Lab balanced: scale reading (expect {expected:F3} N)", scaleReading, expected, 0.03f);
                CheckAtMost("Lab balanced: ring offset in metres (expect <= post 0.01)", ringOffset, post + 0.0005f);
            }
            else
            {
                CheckAtMost("Lab slack scale: reading in newtons (expect 0)", scaleReading, 0.05f);
                Check("Lab slack scale: ring pressed against post (expect 0.01 m)", ringOffset, post, 0.05f);
            }
        }

        // The drawn slack arc must be as long as the rope.
        private static void ArcLength()
        {
            float[][] cases = { new[] { 0.5f, 0.6f }, new[] { 0.3f, 0.6f }, new[] { 0.1f, 0.6f }, new[] { 0.55f, 0.5501f } };
            foreach (float[] pair in cases)
            {
                var points = new Vector3[64];
                RopeArc.Fill(Vector3.zero, new Vector3(pair[0], 0f, 0f), pair[1], Vector3.forward, points);
                float total = 0f;
                for (int i = 1; i < points.Length; i++) total += Vector3.Distance(points[i - 1], points[i]);
                // A polyline is a little shorter than the true arc. 0.1 percent is the allowed gap.
                Check($"Arc chord {pair[0]} length {pair[1]} (expect ratio 1.000)", total / pair[1], 1f, 0.001f);
            }
        }

        private static void CheckAtMost(string name, float actual, float limit)
        {
            bool ok = actual <= limit;
            if (!ok) _failures++;
            Report.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}: got {actual:F4}");
        }

        private static void Check(string name, float actual, float expected, float tolerance)
        {
            // Relative tolerance for real numbers, absolute for values near zero.
            float allowed = Mathf.Max(tolerance * Mathf.Abs(expected), tolerance * 0.5f);
            if (expected == 0f) allowed = tolerance;
            bool ok = Mathf.Abs(actual - expected) <= allowed;
            if (!ok) _failures++;
            Report.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}: got {actual:F4}");
        }
    }
}
