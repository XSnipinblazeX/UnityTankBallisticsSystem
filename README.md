## ⚠️ **Open Source Ballistics System: Study Model**

Due to request on Discord and my urge to help and perhaps be helped in return, I am deciding to open source **MY ballistics system**. Unfortunately, it's been too long in development to keep it clean, so it's been intertwined with other assets and systems I also have made. So no, there's no real way to copy this, and it's more of a **pure study model**.

Here's how it works:

* **External Ballistics:** The flight of the projectile through the air, accounting for gravity, air resistance, and advanced aerodynamic effects.
* **Terminal Ballistics:** The interaction of the projectile with an armored or unarmored target, including penetration, ricochet, shatter, and post-impact effects.
* **Post-Penetration Effects:** The consequences of a successful penetration, such as spalling, fragmentation, and internal explosions.

The system is highly **data-driven**, using ScriptableObject assets (**ProjectileDataCodex**, **ArmorCodex**) to define the characteristics of projectiles and armor, allowing for easy creation and modification of different ammunition and target types.

---

## 1. 🚀 **External Ballistics: The Flight Model**

The flight of the projectile is managed by the `Bullet.cs` script. It uses an iterative, sub-step physics model within `FixedUpdate` to ensure accuracy, even at high velocities.

### **Basic Physics**

* **Gravity:** A simple gravity modifier (`GravityModifier`) is applied, allowing for adjustment of gravitational effects.
* **Drag:** The primary force slowing the projectile is aerodynamic drag. The system uses a sophisticated model where the drag is not constant but varies with the projectile's speed relative to the speed of sound (**Mach number**).
    * The `ProjectileData` defines a `DragModel` (G1, G7, or OTHER).
    * The `ExternalBallistics.cs` file contains `SortedList` tables that map Mach number to a drag multiplier for each model.
    * In `Bullet.cs`, the `updateBallisticCoefficient()` method calculates the current Mach number and gets the appropriate drag multiplier. This is then used in `calculateAcceleration()` to compute the drag force, which is proportional to the air density, the projectile's cross-sectional area, and the square of its velocity.

### **Advanced Aerodynamics & Stability**

For long-rod and spin-stabilized projectiles, the simulation goes beyond simple point-mass physics to model the projectile's orientation and stability in flight.

#### **Spin and Gyroscopic Stability:**

* Projectiles are given an initial spin rate (`MuzzleSpinRPS`). This spin provides **gyroscopic stability**, which helps the projectile resist tumbling.
* The `SpinStabilization` method in `Bullet.cs` calculates the interaction between aerodynamic forces and the projectile's spin.
* **Yaw:** As the projectile's trajectory arcs due to gravity, its velocity vector changes, but its physical orientation (due to inertia) tries to stay the same. The angle between the projectile's axis and its velocity vector is the **yaw angle**.
* **Restoring Torque:** Aerodynamic forces create a restoring torque that tries to push the nose of the projectile back in line with its velocity vector. This is governed by the `RestoringCoefficient`.
* **Damping:** The interaction between restoring torque and gyroscopic stability causes a spiral or precessional motion. The `DampingCoefficient` dampens this motion over time.
* **Instability & Tumbling:** If the restoring torque overcomes the gyroscopic stability (controlled by `StabilityThreshold`), the projectile becomes unstable and begins to **tumble** end-over-end (`IsTumbling = true`). A tumbling projectile has drastically reduced penetration capability.

#### **Magnus Effect:**

* A spinning projectile moving through the air creates a pressure differential, resulting in a lift force perpendicular to both the spin axis and the direction of travel.
* The `MagnusAcceleration()` method calculates this force, which causes the projectile to drift sideways. The magnitude is controlled by the `MagnusCoefficient`.

#### **Fin Stabilization:**

* If a projectile is marked as **FinStabilized**, it uses a different `FinStabilization` logic that relies on aerodynamic shape rather than spin to maintain its orientation.

All these forces are integrated in each physics step in `MoveBulletFor()`, which uses a **Velocity Verlet integration method** (`CalculateBulletMotion`) for a stable and accurate simulation of the projectile's path and orientation.

---

## 2. 💥 **Terminal Ballistics: The Impact Model**

When a projectile hits a collider, the `HandleImpactDamage` method in `Bullet.cs` is triggered. This is the core of the terminal ballistics simulation.

### **Hit Detection**

* The system uses a "**thick**" raycast (`SphereCast`) to give the projectile volume, preventing it from passing through thin objects or corners at high speeds. This is handled in `RunHitDetection`.
* It correctly ignores the colliders of the firing vehicle to prevent self-collision.

### **Penetration Formulas**

The system uses different **empirical formulas** based on the `ProjectileType` defined in `ProjectileData`. These formulas are located in the `TerminalBallistics.cs` static class.

#### **Long-Rod Penetrators (APFSDS - ProjectileType.longRod):**

* Uses `PerforationEquation.CalculatePerforation`.
* This is a complex formula that accounts for projectile length (`lw`), diameter (`d`), impact velocity (`v_i`), obliquity, and the densities and hardness of both the penetrator and the target armor (`rho_p`, `rho_t`, `bhnt`, `bhnp`).
* It supports different materials (**Tungsten**, **DU**, **Steel**), each with its own set of empirical coefficients.

#### **Full-Caliber Rounds (AP, APC, APCBC - ProjectileType.fullCaliber):**

* Uses `DeMarrePenetration.CalculatePenetration`.
* This is a variant of the Lanz-Odermatt formula that calculates penetration based on projectile mass, diameter, and impact velocity, scaled by a `DeMarreConstant`.
* A key feature for this round type is **mass loss**. After penetrating a plate, the `CalculateNewMass` function is used to determine the projectile's new, reduced mass, which affects its ability to penetrate subsequent plates.

#### **Chemical Energy Rounds (HE, HEAT - PenetratorType.CH, PenetratorType.JET):**

* **HE (High Explosive):** Uses `HighExplosive.GetPenetration` to calculate penetration based on the explosive mass (`ExplosiveMassKG`). This models the effect of blast pressure against armor.
* **HEAT (High-Explosive Anti-Tank):** If `ShapedCharge` is true, it uses `HighExplosive.HeatPenetration`. This formula models the formation of a **superplastic jet of molten metal**. Penetration is a function of the shell's caliber and explosive mass. Upon successful penetration, a new "jet" projectile is spawned to simulate the effect inside the target.

### **Armor Interaction**

The `Armor.cs` script defines the properties of an armor plate.

* **Effective Thickness:** When a projectile strikes, the `GetLosThickness` method first applies a material **effectiveness multiplier** from `ArmorData`. It then calculates the line-of-sight (LOS) thickness based on the impact angle (`effectiveThickness = thickness / cos(obliquity)`). This provides the final effective armor value for penetration testing.

### **Impact Modifiers & Special Cases**

The simulation includes several critical real-world effects:

* **Yaw Penalty & Shatter:** A projectile hitting with **yaw** has its penetration value reduced by `CalculateYawFactor`. Excessive yaw can cause the projectile to **shatter** on impact.
* **Ricochet:** The chance of a **ricochet** at high impact angles is determined by an `AnimationCurve` in `ProjectileData`.
* **Overmatch:** If the projectile's diameter or raw penetration value significantly **overmatches** the armor thickness, penetration is **forced**, and ricochet is nullified.

---

## 3. 💣 **Post-Penetration Effects**

If a projectile successfully penetrates an armor plate, a new set of events occurs.

### **Spalling:**

The impact can cause fragments of the armor itself to break off from the interior face at high velocity. This is called **spall**.

* The `Armor.GenerateSpall` method is called on penetration.
* It creates a cone of `spallPrefab` projectiles (defined in `ProjectileData`). These are new, smaller `Bullet` instances with their own (usually less powerful) `ProjectileData`.
* The number of spall fragments is capped per projectile (`SpallFragmentCap`) to prevent performance issues.

### **Fuses (APHE):**

**Armor-Piercing High-Explosive (APHE)** rounds detonate after penetrating armor.

* The fuse logic in `HandleImpactDamage` is triggered for KE rounds with an `Impact` fuse type.
* Upon penetrating the first plate, the system checks if the path ahead is clear for a set distance (`targetFuseDistance`).
* If the path is clear, the bullet teleports to the fuse distance and detonates using `ExplodeBullet`. If it hits an undeafetable plate, the fuse is a "**dud**," and it continues as a simple kinetic round.

### **Explosions:**

* The `ExplodeBullet` method creates a visual effect and calculates an explosion radius based on the `ExplosiveMassKG`.
* `HandleExplosionDamage` then applies damage to any `Human` or `Compartment` components within that radius, simulating **blast** and **overpressure effects**.

This detailed, multi-layered approach creates a highly realistic and emergent simulation where the outcome of a single shot depends on a complex interplay of physics, material science, and projectile design.

---

## 4. 🚑 **Damage Model: Consequences and Internal Effects**

The vehicle damage model is a comprehensive, physics-based system that simulates the lethal **consequences** of a successful hit, focusing on internal component damage and crew casualties. It is driven by the ballistic outcome from the **Terminal Ballistics** simulation.

The process is divided into two primary stages:

1.  **Armor Interaction:** The outcome of the initial impact (Penetration, Non-Penetration, or Ricochet) is determined by the `HandleImpactDamage` method, as detailed in the **Terminal Ballistics** section (2).
2.  **Internal Damage:** The effects of a projectile or its fragments after they have defeated the outer armor.

### **Internal Damage: Post-Penetration Effects**

Once a projectile or fragment is inside the vehicle, it becomes a lethal hazard to the crew and internal systems. The simulation tracks the projectile's path and energy as it interacts with internal components.

#### **Damage Sources**

* **The Penetrator Itself:** The original projectile continues to travel inside the vehicle after penetration, albeit with reduced velocity and, for some types, reduced mass. It can hit multiple components until it runs out of energy or exits.
* **Spalling:** A successful kinetic penetration violently ejects fragments from the interior face of the armor plate.
    * `Armor.GenerateSpall` creates a cone of new, smaller `Bullet` projectiles. These spall fragments travel at high velocity, creating a deadly cone of secondary projectiles that can damage multiple modules and crew members. *Even a non-penetrating hit can cause spalling if the impact energy is sufficiently high.*
* **Explosions (HE, APHE, HEAT):**
    * **HE/HEAT:** These rounds create a jet (HEAT) or blast/fragmentation effect (HE) inside after detonating on the outer armor.
    * **APHE (Armor-Piercing High-Explosive):** These rounds detonate after penetrating armor, as determined by the fuse logic.

#### **Damaging Components**

The projectile and its secondary effects can hit three types of internal components:

* **Modules (`Module.cs`):** These represent vehicle equipment like the engine, ammo rack, fuel tanks, etc.
    * Each module has its own health pool. Damage is based on the projectile's kinetic energy.
    * Modules have properties like `StopBullet` and `StopSpall` to model the difference between "hard" components (engine block) and "soft" components (radio).
    * When a module is destroyed, it triggers cascading effects on the parent vehicle (e.g., engine destruction stops movement, ammo rack destruction causes a catastrophic explosion).
* **Crew (`Human.cs`):** These represent the vehicle's crew members (Gunner, Driver, etc.).
    * **Direct Hits:** A hit performs a raycast through the crew member's body, damaging `Organ` sub-components in its path.
    * **Blast/Overpressure:** Explosions trigger `OnShockwave()` for crew members within the blast radius, simulating internal injuries from blast overpressure.
    * A dead crew member sends a `DamageRelay` to the parent vehicle, disabling the functions associated with that role.
* **Compartments (`Compartment.cs`):** These are abstract volumes within the vehicle.
    * When a projectile enters a compartment, it can trigger an internal `Explosion` based on the projectile's properties, simulating overpressure in a confined space.

This layered and event-driven damage model creates a highly dynamic and realistic simulation, where a single shot can have a cascading effect resulting in a catastrophic kill.
