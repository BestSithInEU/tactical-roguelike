using System;

namespace TacticalRoguelike.Core
{
    public sealed class EntityState
    {
        public EntityState(string id, GridPosition position, int maxHitPoints, int attackDamage)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Entity id is required.", nameof(id));
            }

            if (maxHitPoints <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxHitPoints),
                    maxHitPoints,
                    "Max hit points must be greater than zero."
                );
            }

            if (attackDamage < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attackDamage),
                    attackDamage,
                    "Attack damage cannot be negative."
                );
            }

            Id = id;
            Position = position;
            HomePosition = position;
            MaxHitPoints = maxHitPoints;
            HitPoints = maxHitPoints;
            AttackDamage = attackDamage;
        }

        internal EntityState(
            string id,
            GridPosition position,
            int maxHitPoints,
            int hitPoints,
            int attackDamage,
            bool isAlerted,
            GridPosition? lastKnownPlayerPosition,
            int searchTurnsRemaining
        )
            : this(
                id,
                position,
                maxHitPoints,
                hitPoints,
                attackDamage,
                isAlerted,
                lastKnownPlayerPosition,
                searchTurnsRemaining,
                position,
                false,
                0
            ) { }

        internal EntityState(
            string id,
            GridPosition position,
            int maxHitPoints,
            int hitPoints,
            int attackDamage,
            bool isAlerted,
            GridPosition? lastKnownPlayerPosition,
            int searchTurnsRemaining,
            GridPosition homePosition,
            bool isReturningHome,
            int patrolStepIndex
        )
            : this(id, position, maxHitPoints, attackDamage)
        {
            if (hitPoints < 0 || hitPoints > maxHitPoints)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hitPoints),
                    hitPoints,
                    "Hit points must be between zero and max hit points."
                );
            }
            if (searchTurnsRemaining < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(searchTurnsRemaining),
                    searchTurnsRemaining,
                    "Search turns cannot be negative."
                );
            }

            HitPoints = hitPoints;
            HomePosition = homePosition;
            IsAlerted = isAlerted && hitPoints > 0;
            LastKnownPlayerPosition = IsAlerted ? lastKnownPlayerPosition : null;
            SearchTurnsRemaining = IsAlerted ? searchTurnsRemaining : 0;
            IsReturningHome = !IsAlerted && hitPoints > 0 && isReturningHome;
            PatrolStepIndex = Math.Max(0, patrolStepIndex);
        }

        public string Id { get; }
        public GridPosition Position { get; set; }
        public int MaxHitPoints { get; }
        public int HitPoints { get; private set; }
        public int AttackDamage { get; }
        public bool IsAlive => HitPoints > 0;
        public GridPosition HomePosition { get; private set; }
        public bool IsAlerted { get; private set; }
        public GridPosition? LastKnownPlayerPosition { get; private set; }
        public int SearchTurnsRemaining { get; private set; }
        public bool IsReturningHome { get; private set; }
        public int PatrolStepIndex { get; private set; }

        public void ObservePlayer(GridPosition playerPosition, int searchTurns)
        {
            if (searchTurns < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(searchTurns),
                    searchTurns,
                    "Search turns cannot be negative."
                );
            }

            IsAlerted = true;
            LastKnownPlayerPosition = playerPosition;
            SearchTurnsRemaining = searchTurns;
            IsReturningHome = false;
        }

        public void SpendSearchTurn()
        {
            if (SearchTurnsRemaining > 0)
            {
                SearchTurnsRemaining--;
            }

            if (SearchTurnsRemaining == 0)
            {
                BeginReturnHome();
            }
        }

        public void BeginReturnHome()
        {
            IsAlerted = false;
            LastKnownPlayerPosition = null;
            SearchTurnsRemaining = 0;
            IsReturningHome = IsAlive;
        }

        public void FinishReturnHome()
        {
            IsReturningHome = false;
        }

        public void AdvancePatrolStep(int patrolPointCount)
        {
            if (patrolPointCount <= 0)
            {
                PatrolStepIndex = 0;
                return;
            }

            PatrolStepIndex = (PatrolStepIndex + 1) % patrolPointCount;
        }

        public void ForgetPlayer()
        {
            IsAlerted = false;
            LastKnownPlayerPosition = null;
            SearchTurnsRemaining = 0;
            IsReturningHome = false;
        }

        public void TakeDamage(int damage)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damage),
                    damage,
                    "Damage cannot be negative."
                );
            }

            if (!IsAlive)
            {
                return;
            }

            HitPoints = Math.Max(0, HitPoints - damage);
            if (!IsAlive)
            {
                ForgetPlayer();
            }
        }
    }
}
