using System;

namespace TacticalRoguelike.Core
{
    public static class CombatSystem
    {
        public static bool Attack(EntityState attacker, EntityState defender)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (defender == null)
            {
                throw new ArgumentNullException(nameof(defender));
            }

            if (!attacker.IsAlive || !defender.IsAlive)
            {
                return false;
            }

            defender.TakeDamage(attacker.AttackDamage);
            return true;
        }
    }
}
