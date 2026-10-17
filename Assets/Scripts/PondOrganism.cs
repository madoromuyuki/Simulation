using UnityEngine;
using UnityEngine.Rendering;

//每个生物独立运行，负责进食、大小、寿命和移动。
public partial class PondOrganism : MonoBehaviour
{
    public TidepoolEvolution.Species species;
    public TidepoolEvolution.State state;
    public float age;
    public float energy = 60;
    public float lifespan;
    //所有数组都按藻类、虾、鱼排列；大小按小、中、大排列。
    static readonly float[] Lifespans = { 20, 45, 90 };
    static readonly float[] EnergyCosts = { .3f, .28f, .38f };
    static readonly float[] SizeScales = { .5f, .75f, 1 };
    [SerializeField, Range(1, 3)] int algaeLevel = 3;
    public int AlgaeLevel => algaeLevel;
    [SerializeField, Range(1, 3)] int animalLevel = 1;
    public int AnimalLevel => animalLevel;
    public float algaeSplitCooldown;
    public float grazingCooldown;
    public SpriteRenderer body;

    TidepoolEvolution pond;
    Vector3 direction;
    float turnTimer;
    float babyTimer;
    float breedingTime;
    float speed;
    float wallEscapeTimer;
    float escapeSide = 1;
    bool dead;
    bool initialized;
    float deltaTime;
    Vector3 baseScale;
    SortingGroup visualGroup;
    CircleCollider2D bodyCollider;
    public float CollisionRadius => Mathf.Max(body.sprite.bounds.extents.x * Mathf.Abs(body.transform.lossyScale.x), body.sprite.bounds.extents.y * Mathf.Abs(body.transform.lossyScale.y)) + .025f;
    public bool Dead => dead;
    public void Initialize(TidepoolEvolution owner)
    {
        if (initialized) return;
        initialized = true; pond = owner;
        age = 0; energy = 60;
        baseScale = transform.localScale;
        ApplySize();
        ConfigureVisuals();
        pond.population.Add(this);
        lifespan = Lifespans[(int)species];
        direction = Random.insideUnitCircle.normalized;
    }
    public void SetAlgaeLevel(int level)
    {
        algaeLevel = Mathf.Clamp(level, 1, 3);
        ApplySize();
    }
    public void SetAnimalLevel(int level)
    {
        animalLevel = Mathf.Clamp(level, 1, 3);
        ApplySize();
        transform.position = ClampBody(transform.position);
    }
    void ApplySize()
    {
        int level = species == TidepoolEvolution.Species.Algae ? algaeLevel : animalLevel;
        transform.localScale = baseScale * SizeScales[level - 1];
        if (bodyCollider != null)
            bodyCollider.radius = CollisionRadius / Mathf.Max(.001f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
    }
    public bool Graze()
    {
        if (dead || species != TidepoolEvolution.Species.Algae || grazingCooldown > 0) return false;
        grazingCooldown = 1;
        if (algaeLevel == 1) Die();
        else SetAlgaeLevel(algaeLevel - 1);
        return true;
    }
    void OnDestroy() => pond?.population.Remove(this);

    void Start()
    {
        Initialize(FindFirstObjectByType<TidepoolEvolution>());
        if (species == TidepoolEvolution.Species.Algae) body.color = Color.green;
        if (species == TidepoolEvolution.Species.Shrimp) body.color = new Color(1, .7f, .3f);
        if (species == TidepoolEvolution.Species.Fish) body.color = Color.cyan;
    }

    void Update()
    {
        if (dead || pond == null) return;
        deltaTime = pond.Step;
        if (deltaTime <= 0) return;
        algaeSplitCooldown = Mathf.Max(0, algaeSplitCooldown - deltaTime);
        grazingCooldown = Mathf.Max(0, grazingCooldown - deltaTime);
        age += deltaTime;
        babyTimer += deltaTime;
        breedingTime -= deltaTime;
        energy -= deltaTime * EnergyCosts[(int)species];
        if (pond.oxygen < 30 && species != TidepoolEvolution.Species.Algae) energy -= deltaTime * (30 - pond.oxygen) * .07f;

        if (age >= lifespan || energy <= 0)
        {
            Die(age < lifespan); //寿命到期直接消失，其他死亡仍可能留下营养。
            return;
        }

        //每两秒换一次自由游动方向。
        turnTimer += deltaTime;
        if (turnTimer > 2)
        {
            direction = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
            turnTimer = 0;
        }

        if (species == TidepoolEvolution.Species.Algae) AlgaeBehaviour();
        if (species == TidepoolEvolution.Species.Shrimp) ShrimpBehaviour();
        if (species == TidepoolEvolution.Species.Fish) FishBehaviour();

        ApplySize();
        MoveWithSeparation(deltaTime);
        UpdateVisualDepth();
        if (species != TidepoolEvolution.Species.Algae && direction.sqrMagnitude > .01f)
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    //藻类找营养并分裂；虾啃食藻类；鱼捕食虾。
    void AlgaeBehaviour()
    {
        speed = .24f; //藻类平时的游速。
        state = TidepoolEvolution.State.Drifting;
        if (pond.Light < .23f)
        {
            state = TidepoolEvolution.State.Dormant;
            speed = .07f;
            return;
        }

        energy = Mathf.Min(120, energy + deltaTime * pond.Light * 1.8f);

        Transform target = null;
        float nearest = 4.5f;
        foreach (Transform food in pond.nutrientRoot)
        {
            if (!food.gameObject.activeSelf) continue;
            float distance = Vector3.Distance(transform.position, food.position);
            if (distance < nearest) { target = food; nearest = distance; }
        }
        if (target == null) return;
        direction = (target.position - transform.position).normalized;
        speed = .48f; //藻类靠近营养时的游速。
        if (nearest > CollisionRadius + .12f) return;
        var nutrient = target.GetComponent<PondNutrient>();
        if (nutrient == null) return;
        if (algaeSplitCooldown > 0 || !nutrient.AbsorbForDivision(this)) return;
        state = TidepoolEvolution.State.Growing;
    }

    void ShrimpBehaviour()
    {
        wallEscapeTimer -= deltaTime;
        //速度一快一慢，让虾呈现短促的跳动。
        speed = .6f + Mathf.Sin(pond.Elapsed * 8) * .25f;
        state = TidepoolEvolution.State.Feeding;
        PondOrganism fish = energy > 25 ? FindNearby(TidepoolEvolution.Species.Fish, 1.2f) : null;
        if (fish != null)
        {
            state = TidepoolEvolution.State.Fleeing;
            Vector3 away = (transform.position - fish.transform.position).normalized;
            Vector3 inward = WallAvoidance(transform.position);
            if (inward.sqrMagnitude > .01f)
            {
                Vector3 tangent = new Vector3(-away.y, away.x, 0);
                if (wallEscapeTimer <= 0)
                {
                    escapeSide = Vector3.Dot(tangent, inward) < 0 ? -1 : 1;
                    wallEscapeTimer = 3;
                }
                tangent *= escapeSide;
                away = (away * .15f + tangent * 2f + inward * .65f).normalized;
            }
            direction = away;
            speed = 1.3f;
            return;
        }
        if (breedingTime > 0)
        {
            state = TidepoolEvolution.State.Breeding;
            speed = .1f;
            return;
        }
        EatNearby(TidepoolEvolution.Species.Algae, 3);
        TryHaveBaby();
    }

    void FishBehaviour()
    {
        speed = .6f;
        state = TidepoolEvolution.State.Wandering;
        if (energy > 85)
        {
            state = TidepoolEvolution.State.Resting;
            speed = .1f;
            TryHaveBaby();
            return;
        }
        //保留最后三只虾；鱼不反复追逐无法吃掉的目标。
        if (pond.Count(TidepoolEvolution.Species.Shrimp) <= 3) return;
        PondOrganism shrimp = FindNearby(TidepoolEvolution.Species.Shrimp, 4);
        if (shrimp == null) return;
        state = TidepoolEvolution.State.Hunting;
        speed = 1.6f;
        EatNearby(TidepoolEvolution.Species.Shrimp, 4);
    }

    PondOrganism FindNearby(TidepoolEvolution.Species foodType, float range)
    {
        PondOrganism nearest = null;
        foreach (var other in pond.population)
        {
            if (other == null || other == this || other.dead || other.species != foodType) continue;
            float distance = Vector3.Distance(transform.position, other.transform.position);
            if (distance >= range) continue;
            nearest = other;
            range = distance;
        }
        return nearest;
    }

    void EatNearby(TidepoolEvolution.Species foodType, float range)
    {
        PondOrganism food = FindNearby(foodType, range);
        if (food == null) return;
        Vector3 targetDirection = (food.transform.position - transform.position).normalized;
        direction = Vector3.Lerp(direction, targetDirection, deltaTime * 3).normalized;
        float reach = CollisionRadius + food.CollisionRadius + .06f + speed * deltaTime;
        if (Vector3.Distance(transform.position, food.transform.position) >= reach) return;
        if (foodType == TidepoolEvolution.Species.Algae)
        {
            if (!food.Graze()) return;
        }
        else
        {
            if (pond.Count(foodType) <= 3) return;
            food.Die();
        }
        energy = Mathf.Min(120, energy + 35);
        age = 0; //只有真正吃到食物，才刷新寿命并长大一级。
        SetAnimalLevel(animalLevel + 1);
    }

    void TryHaveBaby()
    {
        if (species == TidepoolEvolution.Species.Algae || age <= 15 || energy <= 90 || babyTimer <= 20) return;
        if (!pond.MakeBaby(this)) return;
        energy = 55;
        babyTimer = 0;
        breedingTime = 3;
    }

    void Die(bool recycle = true)
    {
        if (dead) return;
        dead = true;
        pond.deaths++;
        if (recycle && Random.value < .45f) pond.AddFood(transform.position);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
