using UnityEngine;

// Every organism runs this script on its own GameObject.
public class PondOrganism : MonoBehaviour
{
    public TidepoolEvolution.Species species;
    public TidepoolEvolution.State state;
    public float age;
    public float energy = 60;
    public float lifespan = 100;
    public SpriteRenderer body;
    public SpriteRenderer stateMarker;

    TidepoolEvolution pond;
    Vector3 direction;
    float turnTimer;
    float babyTimer;
    float breedingTime;
    float speed;
    bool dead;

    void Start()
    {
        pond = FindFirstObjectByType<TidepoolEvolution>();
        age = 0;
        energy = 60;
        if (species == TidepoolEvolution.Species.Algae) lifespan = 90;
        if (species == TidepoolEvolution.Species.Shrimp) lifespan = 130;
        if (species == TidepoolEvolution.Species.Fish) lifespan = 180;
        direction = new Vector3(1, 0, 0);
        if (species == TidepoolEvolution.Species.Algae) body.color = Color.green;
        if (species == TidepoolEvolution.Species.Shrimp) body.color = new Color(1, .7f, .3f);
        if (species == TidepoolEvolution.Species.Fish) body.color = Color.cyan;
        stateMarker.gameObject.SetActive(false);
    }

    void Update()
    {
        if (dead) return;
        age += Time.deltaTime;
        babyTimer += Time.deltaTime;
        breedingTime -= Time.deltaTime;
        energy -= Time.deltaTime;

        if (energy <= 0 || age > lifespan)
        {
            Die();
            return;
        }

        // Pick a new wandering direction every two seconds.
        turnTimer += Time.deltaTime;
        if (turnTimer > 2)
        {
            direction = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
            turnTimer = 0;
        }

        if (species == TidepoolEvolution.Species.Algae) AlgaeBehaviour();
        if (species == TidepoolEvolution.Species.Shrimp) ShrimpBehaviour();
        if (species == TidepoolEvolution.Species.Fish) FishBehaviour();

        transform.position += direction * speed * Time.deltaTime;
        KeepInsidePond();
        float size = Mathf.Min(1, .5f + age / 30);
        transform.localScale = Vector3.one * size;
    }

    void AlgaeBehaviour()
    {
        speed = .12f;
        state = TidepoolEvolution.State.Drifting;
        if (pond.temperature < 15 || pond.temperature > 29)
        {
            state = TidepoolEvolution.State.Dormant;
            speed = .02f;
            return;
        }

        energy += Time.deltaTime * 1.2f;
        foreach (Transform food in pond.nutrientRoot)
        {
            if (Vector3.Distance(transform.position, food.position) < 1.5f)
            {
                state = TidepoolEvolution.State.Growing;
                energy += Time.deltaTime * 3;
                TryHaveBaby();
                break;
            }
        }
    }

    void ShrimpBehaviour()
    {
        // The changing speed makes shrimp move in little hops.
        speed = .6f + Mathf.Sin(Time.time * 8) * .25f;
        state = TidepoolEvolution.State.Feeding;
        PondOrganism fish = FindNearby(TidepoolEvolution.Species.Fish, 2);
        if (fish != null)
        {
            state = TidepoolEvolution.State.Fleeing;
            direction = (transform.position - fish.transform.position).normalized;
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
        PondOrganism shrimp = FindNearby(TidepoolEvolution.Species.Shrimp, 4);
        if (shrimp != null)
        {
            state = TidepoolEvolution.State.Hunting;
            speed = 1.2f;
            EatNearby(TidepoolEvolution.Species.Shrimp, 4);
        }
    }

    PondOrganism FindNearby(TidepoolEvolution.Species foodType, float distance)
    {
        PondOrganism nearest = null;
        foreach (Transform other in pond.organismRoot)
        {
            PondOrganism organism = other.GetComponent<PondOrganism>();
            if (organism == this || organism.dead || organism.species != foodType) continue;
            float gap = Vector3.Distance(transform.position, other.position);
            if (gap < distance)
            {
                nearest = organism;
                distance = gap;
            }
        }
        return nearest;
    }

    void EatNearby(TidepoolEvolution.Species foodType, float range)
    {
        PondOrganism food = FindNearby(foodType, range);
        if (food == null) return;
        Vector3 targetDirection = (food.transform.position - transform.position).normalized;
        direction = Vector3.Lerp(direction, targetDirection, Time.deltaTime * 3).normalized;
        if (Vector3.Distance(transform.position, food.transform.position) < .25f)
        {
            food.Die();
            energy = Mathf.Min(120, energy + 35);
        }
    }

    void TryHaveBaby()
    {
        if (age > 15 && energy > 90 && babyTimer > 20)
        {
            pond.MakeBaby(this);
            energy = 55;
            babyTimer = 0;
            breedingTime = 3;
        }
    }

    void KeepInsidePond()
    {
        Vector3 position = transform.position;
        if (position.x < -10.3f || position.x > 4.5f) direction.x = -direction.x;
        if (position.y < -5.6f || position.y > 5f) direction.y = -direction.y;
        position.x = Mathf.Clamp(position.x, -10.3f, 4.5f);
        position.y = Mathf.Clamp(position.y, -5.6f, 5f);
        transform.position = position;
    }

    void Die()
    {
        dead = true;
        Destroy(gameObject);
    }
}
