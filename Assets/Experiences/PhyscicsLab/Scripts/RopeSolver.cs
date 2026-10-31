using UnityEngine;

namespace PhysicsLab.Lab.Rope
{
    /// <summary>
    /// What the rope solver needs to know about one body for one physics step.
    /// A plain struct, so the maths can be tested without any Unity object.
    /// </summary>
    public struct RopeBodyState
    {
        /// <summary>Current velocity in m/s. The solver edits it.</summary>
        public Vector3 velocity;

        /// <summary>1 divided by mass. 0 means the rope cannot move this body (held, kinematic, frozen).</summary>
        public float inverseMass;

        /// <summary>Per axis: 1 = the body can move on this axis, 0 = frozen (Rigidbody constraints).</summary>
        public Vector3 freeAxes;

        /// <summary>Acceleration that PhysX will add this step (already 0 on frozen axes, 0 if gravity is off).</summary>
        public Vector3 gravity;

        /// <summary>World position of the body in metres. The length fix and the circle limit use it.</summary>
        public Vector3 position;

        /// <summary>True when the body must stay inside a flat circle (the ring at the post, a part at the table reach).</summary>
        public bool hasLimit;

        /// <summary>Centre of that circle (only x and z count).</summary>
        public Vector3 limitCenter;

        /// <summary>Radius of that circle in metres.</summary>
        public float limitRadius;
    }

    /// <summary>
    /// One rope between two bodies. The rope can pass over fixed guide points (the top of a pulley wheel).
    /// It has a fixed length and can only pull, never push.
    /// </summary>
    public sealed class RopeLink
    {
        public int bodyA;
        public int bodyB;
        public Vector3 pointA;
        public Vector3 pointB;
        public Vector3[] guides = new Vector3[0];
        public float length = 1f;

        /// <summary>Where the rope is tied, minus the body position. It lets the length fix move the knot with its body.</summary>
        public Vector3 offsetA;
        public Vector3 offsetB;

        /// <summary>Total impulse (N*s) pushed into the rope this step. Always 0 or negative: a string only pulls.</summary>
        public float impulseSum;

        /// <summary>Tension in newtons, from the last Solve call.</summary>
        public float tension;

        /// <summary>Path length minus rope length, in metres. Negative = slack, positive = overstretched.</summary>
        public float overshoot;
    }

    /// <summary>
    /// Velocity-level rope constraint solver (sequential impulses). Call Solve once per physics step,
    /// before PhysX runs. It keeps the path length of every rope at or below its length.
    /// </summary>
    public static class RopeSolver
    {
        private const float MinLength = 1e-6f;
        private const float OverstretchFix = 0.2f; // share of an overstretch removed per step
        private const float ProjectionToleranceMetres = 0.0002f; // a rope this much too long is left alone

        /// <summary>Total length of the rope path: A to the guides to B.</summary>
        public static float PathLength(RopeLink link)
        {
            Vector3 previous = link.pointA;
            float total = 0f;
            for (int i = 0; i < link.guides.Length; i++)
            {
                total += Vector3.Distance(previous, link.guides[i]);
                previous = link.guides[i];
            }
            return total + Vector3.Distance(previous, link.pointB);
        }

        /// <summary>
        /// Solves all ropes for one step of length dt. Edits bodies[i].velocity.
        /// iterations: 10 to 12 is enough for a few ropes on one ring.
        /// warmStart: starts from last step's impulse, so a rope that holds a weight keeps its tension.
        /// </summary>
        public static void Solve(RopeBodyState[] bodies, RopeLink[] links, int linkCount,
                                 float dt, int iterations, bool warmStart)
        {
            // 1. Predict velocities as PhysX will see them: velocity plus gravity.
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].inverseMass > 0f) bodies[i].velocity += bodies[i].gravity * dt;
            }

            // 2. Fixed data per rope. Positions do not change during the iterations.
            Vector3[] dirA = new Vector3[linkCount];
            Vector3[] dirB = new Vector3[linkCount];
            float[] effective = new float[linkCount];
            float[] maxRate = new float[linkCount];
            for (int k = 0; k < linkCount; k++)
            {
                PrepareLink(bodies, links[k], dt, out dirA[k], out dirB[k], out effective[k], out maxRate[k]);
                if (!warmStart) links[k].impulseSum = 0f;
                else if (effective[k] > 0f) ApplyImpulse(bodies, links[k], dirA[k], dirB[k], links[k].impulseSum);
            }

            // 3. Iterate: each rope removes the part of the closing speed that is not allowed.
            for (int it = 0; it < iterations; it++)
            {
                for (int k = 0; k < linkCount; k++)
                {
                    if (effective[k] <= 0f) continue;
                    RopeLink link = links[k];
                    float rate = Vector3.Dot(dirA[k], bodies[link.bodyA].velocity)
                               + Vector3.Dot(dirB[k], bodies[link.bodyB].velocity);
                    float wanted = (maxRate[k] - rate) / effective[k];
                    float next = Mathf.Min(0f, link.impulseSum + wanted); // a rope never pushes
                    float delta = next - link.impulseSum;
                    link.impulseSum = next;
                    ApplyImpulse(bodies, link, dirA[k], dirB[k], delta);
                }

                // The post (or the table reach) pushes back, so a taut rope cannot drag the ring away through it.
                for (int i = 0; i < bodies.Length; i++) LimitSpeedToCircle(ref bodies[i], dt);
            }

            // 4. Tension = impulse per second. Then undo the gravity prediction (PhysX adds it again).
            for (int k = 0; k < linkCount; k++) links[k].tension = -links[k].impulseSum / dt;
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].inverseMass > 0f) bodies[i].velocity -= bodies[i].gravity * dt;
            }
        }

        /// <summary>
        /// Keeps a limited body inside its circle for the coming step. If the body would end the step outside,
        /// its speed is changed so that it ends on the circle edge instead. It slides along the edge, never through it.
        /// </summary>
        private static void LimitSpeedToCircle(ref RopeBodyState body, float dt)
        {
            if (!body.hasLimit || body.inverseMass <= 0f) return;

            Vector3 flat = new Vector3(1f, 0f, 1f);
            Vector3 offset = Vector3.Scale(body.position - body.limitCenter, flat);
            Vector3 flatVelocity = Vector3.Scale(body.velocity, flat);

            Vector3 offsetAfterStep = offset + flatVelocity * dt;
            float distanceAfterStep = offsetAfterStep.magnitude;
            if (distanceAfterStep <= body.limitRadius) return;

            Vector3 onEdge = offsetAfterStep / distanceAfterStep * body.limitRadius;
            body.velocity += (onEdge - offset) / dt - flatVelocity;
        }

        /// <summary>
        /// Position fix, run after the speeds are solved. Moves bodies so that no rope is longer than its length.
        /// Speeds alone leave a drift, and a large one when the body that should give way cannot move
        /// (the ring held by the post, a part held by a hand). This closes the gap every step.
        /// Needs position, offsetA and offsetB to be filled in.
        /// </summary>
        public static void ProjectLengths(RopeBodyState[] bodies, RopeLink[] links, int linkCount, int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                for (int k = 0; k < linkCount; k++) ProjectLink(bodies, links[k]);
                for (int i = 0; i < bodies.Length; i++) PushInsideCircle(ref bodies[i]);
            }
        }

        private static void ProjectLink(RopeBodyState[] bodies, RopeLink link)
        {
            RopeBodyState a = bodies[link.bodyA];
            RopeBodyState b = bodies[link.bodyB];
            link.pointA = a.position + link.offsetA;
            link.pointB = b.position + link.offsetB;

            float overshoot = PathLength(link) - link.length;
            link.overshoot = overshoot;
            if (overshoot <= ProjectionToleranceMetres) return;

            Vector3 nextAfterA = link.guides.Length > 0 ? link.guides[0] : link.pointB;
            Vector3 lastBeforeB = link.guides.Length > 0 ? link.guides[link.guides.Length - 1] : link.pointA;
            Vector3 moveA = Vector3.Scale(Direction(link.pointA - nextAfterA), a.freeAxes); // this way makes the rope longer
            Vector3 moveB = Vector3.Scale(Direction(link.pointB - lastBeforeB), b.freeAxes);

            float weight = a.inverseMass * moveA.sqrMagnitude + b.inverseMass * moveB.sqrMagnitude;
            if (weight <= 0f) return; // nobody can give way

            float share = overshoot / weight;
            a.position -= moveA * (a.inverseMass * share);
            b.position -= moveB * (b.inverseMass * share);
            bodies[link.bodyA] = a;
            bodies[link.bodyB] = b;
            link.overshoot = 0f;
        }

        private static void PushInsideCircle(ref RopeBodyState body)
        {
            if (!body.hasLimit || body.inverseMass <= 0f) return;

            Vector3 offset = Vector3.Scale(body.position - body.limitCenter, new Vector3(1f, 0f, 1f));
            float distance = offset.magnitude;
            if (distance <= body.limitRadius) return;
            body.position -= offset / distance * (distance - body.limitRadius);
        }

        private static void PrepareLink(RopeBodyState[] bodies, RopeLink link, float dt,
                                        out Vector3 dirA, out Vector3 dirB, out float effective, out float maxRate)
        {
            Vector3 nextAfterA = link.guides.Length > 0 ? link.guides[0] : link.pointB;
            Vector3 lastBeforeB = link.guides.Length > 0 ? link.guides[link.guides.Length - 1] : link.pointA;

            dirA = Direction(link.pointA - nextAfterA); // moving A this way makes the rope longer
            dirB = Direction(link.pointB - lastBeforeB);

            RopeBodyState a = bodies[link.bodyA];
            RopeBodyState b = bodies[link.bodyB];
            effective = a.inverseMass * MaskedSqr(dirA, a.freeAxes) + b.inverseMass * MaskedSqr(dirB, b.freeAxes);

            link.overshoot = PathLength(link) - link.length;
            maxRate = link.overshoot <= 0f ? -link.overshoot / dt : -OverstretchFix * link.overshoot / dt;
        }

        private static void ApplyImpulse(RopeBodyState[] bodies, RopeLink link, Vector3 dirA, Vector3 dirB, float impulse)
        {
            RopeBodyState a = bodies[link.bodyA];
            RopeBodyState b = bodies[link.bodyB];
            a.velocity += Vector3.Scale(dirA, a.freeAxes) * (a.inverseMass * impulse);
            b.velocity += Vector3.Scale(dirB, b.freeAxes) * (b.inverseMass * impulse);
            bodies[link.bodyA] = a;
            bodies[link.bodyB] = b;
        }

        private static float MaskedSqr(Vector3 direction, Vector3 axes)
        {
            return Vector3.Scale(direction, axes).sqrMagnitude;
        }

        private static Vector3 Direction(Vector3 v)
        {
            float length = v.magnitude;
            return length < MinLength ? Vector3.zero : v / length;
        }
    }

    /// <summary>Drawing maths for a rope that is not taut: a circular arc of fixed length.</summary>
    public static class RopeArc
    {
        /// <summary>
        /// Half angle theta of a circular arc with the given chord and arc length.
        /// It solves sin(theta) / theta = chord / length. Returns 0 for a straight rope.
        /// </summary>
        public static float HalfAngle(float chord, float length)
        {
            if (length <= 0f || chord >= length * 0.99999f) return 0f;
            float ratio = Mathf.Max(chord, 0f) / length;

            float low = 1e-5f;
            float high = Mathf.PI;
            for (int i = 0; i < 50; i++)
            {
                float mid = 0.5f * (low + high);
                // sin(t)/t falls from 1 at t = 0 to 0 at t = pi, so one root exists.
                if (Mathf.Sin(mid) / mid > ratio) low = mid; else high = mid;
            }
            return 0.5f * (low + high);
        }

        /// <summary>
        /// Fills points with an arc from start to end. Arc length equals length.
        /// side is the unit direction the arc bulges toward (it is made square to the chord).
        /// </summary>
        public static void Fill(Vector3 start, Vector3 end, float length, Vector3 side, Vector3[] points)
        {
            int last = points.Length - 1;
            Vector3 chordVector = end - start;
            float chord = chordVector.magnitude;
            float theta = HalfAngle(chord, length);

            if (chord < 1e-6f || theta <= 0f)
            {
                for (int i = 0; i <= last; i++) points[i] = Vector3.Lerp(start, end, i / (float)last);
                return;
            }

            Vector3 along = chordVector / chord;
            Vector3 lateral = side - along * Vector3.Dot(side, along);
            lateral = lateral.sqrMagnitude < 1e-8f ? Vector3.zero : lateral.normalized;

            float radius = length / (2f * theta);
            for (int i = 0; i <= last; i++)
            {
                float phi = theta * (2f * i / last - 1f);
                float x = 0.5f * chord + radius * Mathf.Sin(phi);
                float bulge = radius * (Mathf.Cos(phi) - Mathf.Cos(theta));
                points[i] = start + along * x + lateral * bulge;
            }
        }
    }
}
