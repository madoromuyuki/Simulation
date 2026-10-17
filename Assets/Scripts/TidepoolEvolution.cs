using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class TidepoolEvolution : MonoBehaviour
{
    public enum Species { Algae, Shrimp, Fish }
    public enum State { Drifting, Growing, Dormant, Feeding, Fleeing, Breeding, Wandering, Hunting, Resting }
    public Transform organismRoot, nutrientRoot;
    public PondOrganism[] organismPrefabs;
    public GameObject nutrientPrefab;
    public Camera pondCamera;
    public float oxygen = 80;
    public float Elapsed { get; private set; }
    public float Light => .8f * (.6f + .4f * Mathf.Sin(Elapsed / 40));
    public readonly List<PondOrganism> population = new List<PondOrganism>();
    public int births, deaths;
    bool paused, help = true, gameOver;
    public bool IsGameOver => gameOver;
    float rate = 1;
    public float SimulationSpeed => rate;
    static readonly float[] SpeedSteps = { .5f, .75f, 1f, 1.5f, 2f, 3f };
    int SpeedIndex
    {
        get
        {
            int nearest = 0;
            for (int i = 1; i < SpeedSteps.Length; i++)
                if (Mathf.Abs(SpeedSteps[i] - rate) < Mathf.Abs(SpeedSteps[nearest] - rate)) nearest = i;
            return nearest;
        }
    }
    public void SetSimulationSpeed(float value)
    {
        rate = float.IsNaN(value) || float.IsInfinity(value) ? 1 : value;
        rate = SpeedSteps[SpeedIndex];
    }
    public static readonly Color WaterColor = new Color32(16, 47, 54, 255);
    string notice = "";
    public const float Left = -10.3f, Right = 4.5f, Bottom = -5.6f, Top = 5f;
    public float Step => paused || help || gameOver ? 0 : Mathf.Min(Time.deltaTime, .05f) * rate;
    public int Count(Species species)
    {
        int count = 0;
        foreach (var organism in population)
            if (organism != null && !organism.Dead && organism.species == species) count++;
        return count;
    }
    //camera, bg, nurti
    void Start()
    {
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        pondCamera.backgroundColor = WaterColor;
        var water = GameObject.Find("Water");
        if (water != null && water.TryGetComponent<SpriteRenderer>(out var waterRenderer)) waterRenderer.color = WaterColor;
        pondCamera.rect = new Rect(0, .0875f, .73f, .815f);
        pondCamera.transform.position = new Vector3((Left + Right) / 2, (Bottom + Top) / 2, -10);
        pondCamera.orthographicSize = 5.6f;
        foreach (var text in FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) text.gameObject.SetActive(false);
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
        foreach (Transform food in nutrientRoot) if (!food.GetComponent<PondNutrient>()) food.gameObject.AddComponent<PondNutrient>();
        Log("Click to feed. Drag the speed slider to watch the pond evolve.");
    }
    //update every frame
    void Update()
    {
        if (!help && !gameOver && (Count(Species.Algae) == 0 || Count(Species.Shrimp) == 0 || Count(Species.Fish) == 0))
        {
            gameOver = true;
            notice = "";
        }
        float deltaTime = Step;
        Elapsed += deltaTime;
        oxygen = Mathf.Clamp(oxygen + deltaTime * (Count(Species.Algae) * Light * .027f - Count(Species.Shrimp) * .018f - Count(Species.Fish) * .07f + (75 - oxygen) * .035f), 0, 100);
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) paused = !paused;
    }
    //press = feed; when meets limit, delete the oldest nurti
    public bool TryPlaceNutrient(Vector2 screenPosition)
    {
        if (help || gameOver || !pondCamera.pixelRect.Contains(screenPosition)) return false;
        Vector3 position = pondCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -pondCamera.transform.position.z));
        Transform oldest = null;
        int count = 0;
        foreach (Transform food in nutrientRoot)
            if (food.gameObject.activeSelf) { if (oldest == null) oldest = food; count++; }
        if (count >= 24 && oldest != null) { oldest.gameObject.SetActive(false); Destroy(oldest.gameObject); }
        AddFood(position);
        notice = "Nutrient placed. It stays until an alga reaches and absorbs it.";
        return true;
    }
    public void Log(string message) => notice = message;
    public void AddFood(Vector3 position)
    {
        int active = 0;
        foreach (Transform existing in nutrientRoot) if (existing.gameObject.activeSelf) active++;
        if (active >= 24) return;
        var food = Instantiate(nutrientPrefab, Clamp(position), Quaternion.identity, nutrientRoot);
        if (!food.GetComponent<PondNutrient>()) food.AddComponent<PondNutrient>();
        foreach (var renderer in food.GetComponentsInChildren<SpriteRenderer>()) renderer.sortingOrder = 3000;
    }
    //record every org created
    public PondOrganism CreateOrganism(Species species, Vector3 position)
    {
        var organism = Instantiate(organismPrefabs[(int)species], Clamp(position), Quaternion.identity, organismRoot);
        organism.Initialize(this);
        return organism;
    }
    public static Vector3 Clamp(Vector3 p) => new Vector3(Mathf.Clamp(p.x, Left, Right), Mathf.Clamp(p.y, Bottom, Top), 0);
    //small = small + middle; middle = middle + big; big = big + big
    public bool SplitAlgae(PondOrganism parent)
    {
        if (parent == null || parent.Dead || parent.species != Species.Algae || parent.algaeSplitCooldown > 0 || population.Count >= 160 || Count(Species.Algae) >= 95) return false;
        int level = parent.AlgaeLevel;
        var offspring = CreateOrganism(Species.Algae, parent.transform.position);
        offspring.SetAlgaeLevel(Mathf.Min(3, level + 1));
        float spacing = parent.CollisionRadius + offspring.CollisionRadius + .08f;
        float margin = spacing * .5f + Mathf.Max(parent.CollisionRadius, offspring.CollisionRadius);
        Vector3 center = parent.transform.position;
        center.x = Mathf.Clamp(center.x, Left + margin, Right - margin);
        center.y = Mathf.Clamp(center.y, Bottom + margin, Top - margin);
        Vector3 axis = UnityEngine.Random.insideUnitCircle.normalized;
        if (axis.sqrMagnitude < .1f) axis = Vector3.right;
        Vector3 offset = axis * spacing * .5f;
        offspring.transform.position = center + offset;
        offspring.DriftAway(axis);
        parent.DriftAway(-axis);
        offspring.algaeSplitCooldown = parent.algaeSplitCooldown = 1.5f;
        offspring.grazingCooldown = parent.grazingCooldown;
        offspring.energy = parent.energy = 65;
        offspring.age = parent.age = 0;
        parent.transform.position = Clamp(center - offset);
        births++;
        return true;
    }
    public bool MakeBaby(PondOrganism parent)
    {
        if (parent.species == Species.Algae) return false;
        if (population.Count >= 160 || Count(parent.species) >= (parent.species == Species.Shrimp ? 45 : 15)) return false;
        CreateOrganism(parent.species, parent.transform.position + UnityEngine.Random.insideUnitSphere * .4f);
        births++;
        return true;
    }
    //regenerate orgs when restart
    void Restart()
    {
        foreach (var root in new[] { organismRoot, nutrientRoot })
            foreach (Transform child in root)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        population.Clear();
        Elapsed = births = deaths = 0;
        oxygen = 80;
        rate = 1;
        paused = help = gameOver = false;
        int[] startingCounts = { 36, 14, 4 }; //#of algae, shrimp, fish
        for (int species = 0; species < startingCounts.Length; species++)
            for (int count = 0; count < startingCounts[species]; count++)
                CreateOrganism((Species)species, new Vector3(UnityEngine.Random.Range(Left + 1, Right - 1), UnityEngine.Random.Range(Bottom + 1, Top - 1), 0));
        Log("A new pond begins.");
    }
}
