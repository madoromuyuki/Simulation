using UnityEngine;

public class PondNutrient : MonoBehaviour
{
    public float remaining = 55;
    TidepoolEvolution pond;
    float placedAt;
    void Awake() { placedAt = Time.unscaledTime; }
    void Start() { pond = FindFirstObjectByType<TidepoolEvolution>(); }
    public bool AbsorbForDivision(PondOrganism algae)
    {
        if (remaining <= 0 || !gameObject.activeSelf || Time.unscaledTime - placedAt < .5f) return false;
        if (algae == null || Vector3.Distance(algae.transform.position, transform.position) > algae.CollisionRadius + .12f) return false;
        if (pond == null) pond = FindFirstObjectByType<TidepoolEvolution>();
        if (!pond.SplitAlgae(algae)) return false;
        remaining = 0;
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
