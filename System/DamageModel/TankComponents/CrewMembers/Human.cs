using UnityEngine;

public class Human : MonoBehaviour
{

    public enum Role
    {
        Gunner,
        Loader,
        Commander,
        Driver
    }

    public Role role;

    public string NAME = "Tanker";
    public float maxHealth = 100f;
    public float currentHealth;
    public Organ[] organs;
    public LayerMask organLayerMask;

    private void Start()
    {
        currentHealth = maxHealth;
        organs = GetComponentsInChildren<Organ>();
    }


    public void OnShockwave(Vector3 shockwaveOrigin, float shockwaveRadius, float baseShockwaveDamage)
    {
        Debug.Log($"{NAME} received a shockwave!");
        // Iterate through the pre-populated array of organs.
        foreach (Organ organ in organs)
        {
            // Check if the organ is within the shockwave's radius.
            // This is a very fast check using Vector3.Distance.
            float distanceToOrgan = Vector3.Distance(shockwaveOrigin, organ.transform.position);

            if (distanceToOrgan <= shockwaveRadius)
            {
                // Calculate damage with a simple inverse-square falloff model.
                // Damage is highest at the origin and decreases as distance increases.
                float damageFalloff = 1f - (distanceToOrgan / shockwaveRadius);
                float damageToOrgan = baseShockwaveDamage * damageFalloff * organ.damageMultiplier;

                // Apply the damage to the organ.
                organ.TakeDamage(damageToOrgan, this);
            }
        }
    }



    // This is the public method a bullet script will call.
    public void OnHit(float bulletDamage, Vector3 hitPoint, Vector3 bulletDirection)
    {
        // Declare and initialize remainingDamage at the start of the method.
        float remainingDamage = bulletDamage;

        // 1. Perform the internal raycast from the hit point.
        float penetrationDistance = 10f;
        RaycastHit[] internalHits = Physics.RaycastAll(hitPoint, bulletDirection, penetrationDistance, organLayerMask);

        // 2. Process all organs hit by the internal raycast.
        if (internalHits.Length > 0)
        {
            // Sort hits by distance to process them in order.
            System.Array.Sort(internalHits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit internalHit in internalHits)
            {
                Organ hitOrgan = internalHit.collider.GetComponent<Organ>();
                if (hitOrgan != null)
                {
                    float damageToOrgan = remainingDamage * hitOrgan.damageMultiplier;
                    hitOrgan.TakeDamage(damageToOrgan, this);
                    remainingDamage -= damageToOrgan;

                    if (remainingDamage <= 0)
                    {
                        break;
                    }
                }
            }
        }

        // 3. Apply any remaining damage to the overall human health.
        // This line is now outside the if block and can access remainingDamage.
        currentHealth -= remainingDamage;

        // 4. Check for death.
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        DamageRelay dr = new DamageRelay();
        Debug.Log(gameObject.name + " has died.");
        if (role == Role.Commander)
        {
            //commanders dont affect anything
        }
        if (role == Role.Gunner)
        {
            //gunners dont affect anything
            dr.CanShoot = false;
            dr.CanTurret = false;
            GetComponentInParent<BlazeVehicle>().KillTraverse();

        }
        if (role == Role.Loader)
        {
            //loaders dont affect anything
            dr.CanLoad = false;

        }
        if (role == Role.Driver)
        {
            //drivers dont affect anything
            dr.CanDrive = false;
            GetComponentInParent<BlazeVehicle>().KillDriving();
        }
        GetComponentInParent<BlazeVehicle>().ReceiveImpulse(dr);
    }

}