using UnityEngine;
using UnityEngine.InputSystem;

// Sets up the pond and lets the player add food.
public class TidepoolEvolution : MonoBehaviour
{
    public enum Species { Algae, Shrimp, Fish }
    public enum State { Drifting, Growing, Dormant, Feeding, Fleeing, Breeding, Wandering, Hunting, Resting }

    public Transform organismRoot;
    public Transform nutrientRoot;
    public PondOrganism[] organismPrefabs;
    public GameObject nutrientPrefab;
    public Camera pondCamera;
    public float temperature = 22;
    float foodTimer;

    void Start()
    {
        foreach (Transform food in nutrientRoot)
        {
            Destroy(food.gameObject, 45);
        }
    }

    void Update()
    {
        //add a little food automatically every eight seconds.
        foodTimer += Time.deltaTime;
        if (foodTimer > 8)
        {
            Vector3 position = new Vector3(Random.Range(-10f, 4f), Random.Range(-5f, 5f), 0);
            AddFood(position);
            foodTimer = 0;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 position = pondCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            position.z = 0;
            if (position.x > -10.5f && position.x < 4.7f && position.y > -5.8f && position.y < 5.2f)
            {
                AddFood(position);
            }
        }
    }

    public void AddFood(Vector3 position)
    {
        if (nutrientRoot.childCount >= 20) return;
        GameObject food = Instantiate(nutrientPrefab, position, Quaternion.identity, nutrientRoot);
        Destroy(food, 45);
    }

    public void MakeBaby(PondOrganism parent)
    {
        // Stop the pond from getting too crowded.
        if (organismRoot.childCount >= 160) return;
        Vector3 position = parent.transform.position + new Vector3(.2f, .2f, 0);
        PondOrganism baby = Instantiate(organismPrefabs[(int)parent.species], position, Quaternion.identity, organismRoot);
        baby.name = parent.species + " Baby";
        baby.age = 0;
        baby.transform.localScale = Vector3.one * .5f;
    }
}
