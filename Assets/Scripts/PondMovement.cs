using UnityEngine;
using UnityEngine.Rendering;

//spread, eyes and collison
public partial class PondOrganism
{
    //body and eyes as a whole part
    void ConfigureVisuals()
    {
        visualGroup = GetComponent<SortingGroup>();
        if (visualGroup == null) visualGroup = gameObject.AddComponent<SortingGroup>();
        visualGroup.sortingLayerID = body.sortingLayerID;
        UpdateVisualDepth();

        //body size
        bodyCollider = GetComponent<CircleCollider2D>();
        if (bodyCollider == null) bodyCollider = gameObject.AddComponent<CircleCollider2D>();
        bodyCollider.isTrigger = true;
        bodyCollider.radius = CollisionRadius / Mathf.Max(.001f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));

        Transform eye = transform.Find("Eye");
        if (eye != null)
        {
            eye.SetParent(body.transform, true);
            Bounds shape = body.sprite.bounds;
            eye.localPosition = shape.center + Vector3.Scale(shape.extents, new Vector3(.38f, .22f, 0));
            eye.localRotation = Quaternion.identity;
            Vector3 scale = body.transform.localScale;
            eye.localScale = new Vector3(.055f / Mathf.Abs(scale.x), .055f / Mathf.Abs(scale.y), 1);
            var renderer = eye.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.sortingOrder = body.sortingOrder + 2;
        }
    }
    //down = front
    void UpdateVisualDepth() => visualGroup.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 100);
    public void DriftAway(Vector3 heading)
    {
        direction = heading.normalized;
        turnTimer = -1;
    }
    Vector3 AlgaeSeparation()
    {
        Vector3 steering = Vector3.zero;
        foreach (var other in pond.population)
        {
            if (other == null || other == this || other.dead || other.species != TidepoolEvolution.Species.Algae) continue;
            Vector3 away = transform.position - other.transform.position;
            float distance = away.magnitude;
            float personalSpace = CollisionRadius + other.CollisionRadius + .8f;
            if (distance > .001f && distance < personalSpace)
                steering += away / distance * (1 - distance / personalSpace);
        }
        return Vector3.ClampMagnitude(steering, 1);
    }
    Vector3 WallAvoidance(Vector3 position)
    {
        float margin = CollisionRadius + .9f;
        return new Vector3(
            Mathf.Clamp01((TidepoolEvolution.Left + margin - position.x) / .9f) - Mathf.Clamp01((position.x - TidepoolEvolution.Right + margin) / .9f),
            Mathf.Clamp01((TidepoolEvolution.Bottom + margin - position.y) / .9f) - Mathf.Clamp01((position.y - TidepoolEvolution.Top + margin) / .9f), 0);
    }
    Vector3 ClampBody(Vector3 position)
    {
        float radius = CollisionRadius;
        return new Vector3(Mathf.Clamp(position.x, TidepoolEvolution.Left + radius, TidepoolEvolution.Right - radius), Mathf.Clamp(position.y, TidepoolEvolution.Bottom + radius, TidepoolEvolution.Top - radius), 0);
    }
    //split the frame to avoid stuck
    void MoveWithSeparation(float duration)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(duration / .025f));
        float step = duration / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 inward = WallAvoidance(transform.position);
            Vector3 heading = state == TidepoolEvolution.State.Fleeing ? direction : (direction + inward * 1.8f).normalized;
            Vector3 flow = new Vector3(Mathf.Sin(pond.Elapsed * .13f + transform.position.y), Mathf.Cos(pond.Elapsed * .11f + transform.position.x), 0) * .036f;
            Vector3 dispersal = species == TidepoolEvolution.Species.Algae ? AlgaeSeparation() * .32f : Vector3.zero;
            Vector3 next = ClampBody(transform.position + (heading * speed + flow + dispersal) * step);
            for (int pass = 0; pass < 3; pass++)
            foreach (var other in pond.population)
            {
                if (other == null || other == this || other.dead) continue;
                //algae avoids algae
                if ((species == TidepoolEvolution.Species.Algae) != (other.species == TidepoolEvolution.Species.Algae)) continue;
                Vector3 gap = next - other.transform.position;
                float distance = gap.magnitude;
                float clearance = CollisionRadius + other.CollisionRadius + (species == TidepoolEvolution.Species.Algae ? .18f : 0);
                if (distance >= clearance) continue;
                Vector3 normal = distance > .0001f ? gap / distance : (GetInstanceID() < other.GetInstanceID() ? Vector3.left : Vector3.right);
                Vector3 corrected = next + normal * (clearance - distance + .002f);
                next = ClampBody(corrected);
                
                //corner management
                float blocked = Mathf.Max(0, Vector3.Dot(corrected - next, normal));
                if (blocked > .0001f)
                    other.transform.position = other.ClampBody(other.transform.position - normal * (blocked + .005f));
            }
            transform.position = next;
        }
    }

}
