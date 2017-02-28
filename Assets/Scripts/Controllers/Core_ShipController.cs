﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Core_ShipController : MonoBehaviour {

    /* TODO:
     * Implement AIPlayerController & NetworkPlayerController
     *      -When game is started, ONE player controller is assigned to ONE ship 
     *      -Other ships are controlled by AI or through network by other players
     *      
     * Spectator mode
    */

    #region References & variables
    //References
    protected Core_Toolbox toolbox;
    protected Core_GlobalVariableLibrary lib;
    protected Core_EventManager em;
    protected Rigidbody rb;
    protected Vector3 movementDirection;
    protected Vector3 lookTargetPosition;
    Transform shipHull;
    Transform shipTurret;
    Transform turretOutputMarker;
    GameObject healthBar;
    Color myShipColor;
    Core_ShipColorablePartTag[] shipColorableParts;
    List<Core_Projectile> projectileList = new List<Core_Projectile>();

    //Variables coming from within the script
    protected int index = 0; //Set by gameManager when instantiating ships
    float currentHealth = 0; //Set to full by calling Resurrect() when instantiated
    bool isMovable = false;
    bool isVulnerable = false;
    bool canShoot = false;
    bool isDead = false;
    bool shootOnCooldown = false;

    //Values coming from GlobalVariableLibrary
    string shipTag = "Ship";
    string environmentTag = "Environment";
    float movementSpeed = -1;
    protected float maxHealth = -1;
    float shipTurretRotationSpeed = -1;
    float shipHullRotationSpeed = -1;
    float bulletLaunchForce = -1;
    float shootCooldownTime = -1;
    float shootDamage = -1;
    float healthBarMinValue = -1;
    float healthBarMaxValue = -1;
    #endregion

    #region Initialization
    protected virtual void Awake()
    {
        toolbox = FindObjectOfType<Core_Toolbox>();
        lib = toolbox.GetComponentInChildren<Core_GlobalVariableLibrary>();
        em = toolbox.GetComponent<Core_EventManager>();
        rb = GetComponent<Rigidbody>();
        shipColorableParts = GetComponentsInChildren<Core_ShipColorablePartTag>();
        shipHull = GetComponentInChildren<Core_ShipHullTag>().transform;
        shipTurret = GetComponentInChildren<Core_ShipTurretTag>().transform;
        turretOutputMarker = GetComponentInChildren<Core_TurretOutputMarkerTag>().
            transform;
        healthBar = GetComponentInChildren<Core_ShipHealthBarTag>().gameObject;
    }

    protected virtual void GetStats()
    {
        shipTag = lib.shipVariables.shipTag;
        environmentTag = lib.shipVariables.environmentTag;
        movementSpeed = lib.shipVariables.movementSpeed;
        maxHealth = lib.shipVariables.maxHealth;
        shipTurretRotationSpeed = lib.shipVariables.shipTurretRotationSpeed;
        shipHullRotationSpeed = lib.shipVariables.shipHullRotationSpeed;
        bulletLaunchForce = lib.shipVariables.bulletLaunchForce;
        shootCooldownTime = lib.shipVariables.shootCooldownTime;
        shootDamage = lib.shipVariables.shootDamage;
        healthBarMinValue = lib.shipVariables.healthBarMinValue;
        healthBarMaxValue = lib.shipVariables.healthBarMaxValue;
    }

    protected virtual void OnDisable()
    {
        DestroyAllProjectiles();
    }
    #endregion

    #region Update & FixedUpdate
    protected virtual void Update()
    {
        ManageProjectileList();
    }

    protected virtual void FixedUpdate()
    {

        #region Movement
        //TODO: Add lerp to movement?
        if (isMovable && movementDirection != Vector3.zero)
        {
            rb.MovePosition(transform.position + movementDirection * movementSpeed * Time.fixedDeltaTime);
            if (movementDirection == Vector3.zero)
            {
                rb.velocity = Vector3.zero;
            }

            //Hull rotation
            Quaternion newHullRotation = Quaternion.LookRotation(movementDirection);
            shipHull.rotation = Quaternion.Slerp(shipHull.rotation, newHullRotation,
                Time.fixedDeltaTime * shipHullRotationSpeed);
        }
        #endregion

        #region Turret rotation
        lookTargetPosition.y = shipTurret.position.y;
        Vector3 lookDirection = lookTargetPosition - shipTurret.position;
        Quaternion newTurretRotation = Quaternion.LookRotation(lookDirection);
        shipTurret.rotation = Quaternion.Slerp(shipTurret.rotation, newTurretRotation,
            Time.fixedDeltaTime * shipTurretRotationSpeed);
        #endregion
    }
    #endregion

    #region Shooting & projectiles
    private void ManageProjectileList()
    {
        if (projectileList.Count > 0)
        {
            for (int i = 0; i < projectileList.Count; i++)
            {
                Core_Projectile projectile = projectileList[i];
                if (Time.time >= (projectile.GetSpawnTime() + projectile.GetLifeTime()))
                {
                    DestroyProjectile(projectile);
                    i--;
                }
            }
        }
    }

    private void DestroyAllProjectiles()
    {
        int count = projectileList.Count;
        if (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                DestroyProjectile(projectileList[0]);
            }
        }
    }

    private void DestroyProjectile(Core_Projectile projectile)
    {
        projectileList.Remove(projectile);
        if (projectile != null)
            Destroy(projectile.gameObject);
    }

    public void OnProjectileTriggerEnter(Core_Projectile projectile, GameObject collidedObject)
    {
        //Check which object collided with
        //If enemy ship, damage enemyShip
        //Destroy projectile
        //Instantiate effect
        string collidedObjectTag = collidedObject.tag;

        if (collidedObjectTag == shipTag)
        {
            //Damage enemy ship
            collidedObject.GetComponentInParent<Core_ShipController>().TakeDamage(shootDamage);
            //Destroy projectile
            DestroyProjectile(projectile);
        }
        else if (collidedObjectTag == environmentTag)
        {
            DestroyProjectile(projectile);
        }

    }

    protected void Shoot()
    {
        if (canShoot && !shootOnCooldown)
        {
            //Spawn bullet at shipTurret position & rotation
            GameObject newBullet = Instantiate(Resources.Load("Bullet", typeof(GameObject)),
                turretOutputMarker.position, turretOutputMarker.rotation) as GameObject;
            Physics.IgnoreCollision(newBullet.GetComponent<Collider>(), 
                GetComponentInChildren<Collider>());
            newBullet.GetComponent<Rigidbody>().AddForce(newBullet.transform.forward *
                bulletLaunchForce, ForceMode.Impulse);
            Core_Projectile newBulletScript = newBullet.GetComponent<Core_Projectile>();
            newBulletScript.SetProjectileType(Core_Projectile.EProjectileType.BULLET);
            newBulletScript.SetShipController(this);
            newBulletScript.SetProjectileColor(myShipColor);
            projectileList.Add(newBulletScript);

            StartCoroutine(ShootCooldown(shootCooldownTime));
        }
    }

    IEnumerator ShootCooldown(float time)
    {
        shootOnCooldown = true;
        yield return new WaitForSeconds(time);
        shootOnCooldown = false;
    }
    #endregion

    #region Index
    public void GiveIndex(int newIndex)
    {
        if (index == 0)
        {
            index = newIndex;
        }
    }

    //public int GetIndex()
    //{
    //    return index;
    //}
    #endregion

    #region SetVariables
    //protected void SetMovementDirection(Vector3 newMovementDirection)
    //{
    //    movementDirection = newMovementDirection;
    //}

    protected void SetLookTargetPosition(Vector3 newLookTargetPosition)
    {
        lookTargetPosition = newLookTargetPosition;
    }

    protected void SetIsMoveable(bool state)
    {
        isMovable = state;
    }

    protected void SetIsVulnerable(bool state)
    {
        isVulnerable = state;
    }

    protected void SetCanShoot(bool state)
    {
        canShoot = state;
    }

    public void SetShipColor(Color newColor)
    {
        myShipColor = newColor;
        for (int i = 0; i < shipColorableParts.Length; i++)
        {
            //shipColorableParts[i].GetComponent<Renderer>().material.color = newColor;
            shipColorableParts[i].GetComponent<Renderer>().material.SetColor("_TintColor", myShipColor);
        }
    }
    #endregion

    #region Health adjustments
    public void TakeDamage(float amount)
    {
        if (!isDead && isVulnerable)
        {
            //Debug.Log("I'm taking damage.");
            currentHealth -= amount;
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }
            //Update UI
            UpdateHealthBar();
        }
    }

    public void AddHealth(float amount)
    {
        if (!isDead)
        {
            currentHealth += amount;
            if (currentHealth > maxHealth)
            {
                currentHealth = 0;
            }
            //Update UI
            UpdateHealthBar();
        }
    }
    #endregion

    #region Die, Resurrect & SpectatorMode
    private void Die()
    {
        isDead = true;
        isVulnerable = false;
        isMovable = false;
        canShoot = false;
        //Broadcast ship death
        //Start spectator mode if player
        em.BroadcastShipDead(index);
        Destroy(gameObject);
    }

    //protected void Resurrect()
    //{
    //    // TODO: Currently Die-method destroys the object, so resurrect is unneccessary
    //    //      Remove if still obsolete in the future (currently only used for setting isDead 
    //    //      to true when initializing ships)
    //    Debug.Log("Resurrecting");
    //    //Reset all stats
    //    isDead = false;
    //    AddHealth(maxHealth);
    //}
    #endregion

    #region Worldspace UI
    private void UpdateHealthBar()
    {
        // TODO: Add lerp
        float healthBarFillAmount = 1 - (currentHealth / maxHealth);
        healthBar.GetComponent<Renderer>().material.SetFloat("_Cutoff",
            Mathf.Clamp(healthBarFillAmount, healthBarMinValue, healthBarMaxValue));
    }
    #endregion

    #region Collision detection
    //private void OnCollisionEnter()
    //{
    //    Debug.Log("OnCollisionEnter");
    //}
    #endregion
}
