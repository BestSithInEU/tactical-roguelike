using NUnit.Framework;
using TacticalRoguelike.Core;

namespace TacticalRoguelike.Tests.EditMode
{
    public sealed class CombatSystemTests
    {
        [Test]
        public void Attack_LivingDefender_AppliesIntegerDamage()
        {
            var attacker = new EntityState("attacker", new GridPosition(0, 0), 5, 2);
            var defender = new EntityState("defender", new GridPosition(1, 0), 5, 1);

            bool attacked = CombatSystem.Attack(attacker, defender);

            Assert.IsTrue(attacked);
            Assert.AreEqual(3, defender.HitPoints);
            Assert.IsTrue(defender.IsAlive);
        }

        [Test]
        public void Attack_DamageReachesHitPoints_KillsDefender()
        {
            var attacker = new EntityState("attacker", new GridPosition(0, 0), 5, 3);
            var defender = new EntityState("defender", new GridPosition(1, 0), 3, 1);

            CombatSystem.Attack(attacker, defender);

            Assert.AreEqual(0, defender.HitPoints);
            Assert.IsFalse(defender.IsAlive);
        }

        [Test]
        public void Attack_DeadAttacker_DoesNotDamageDefender()
        {
            var attacker = new EntityState("attacker", new GridPosition(0, 0), 1, 3);
            var defender = new EntityState("defender", new GridPosition(1, 0), 5, 1);
            attacker.TakeDamage(1);

            bool attacked = CombatSystem.Attack(attacker, defender);

            Assert.IsFalse(attacked);
            Assert.AreEqual(5, defender.HitPoints);
        }
    }
}
