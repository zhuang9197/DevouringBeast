using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class BelieverFollower : MonoBehaviour
    {
        private static readonly List<BelieverFollower> Active = new();
        private Transform _player;
        private PlayerSpit _spit;
        private SpriteRenderer _renderer;
        private float _nextAttack;
        private bool _baptized;
        private float _phase;
        private Sprite _front, _side, _back;
        private static RogueSkillCatalog Catalog;

        public bool IsBaptized => _baptized;
        public static void Configure(RogueSkillCatalog catalog) => Catalog = catalog;

        public static BelieverFollower SpawnFollower(Transform player, bool baptized)
        {
            if (player == null) return null;
            GameObject go = new("BelieverFollower", typeof(SpriteRenderer), typeof(BelieverFollower));
            BelieverFollower follower = go.GetComponent<BelieverFollower>();
            follower._player = player;
            follower._spit = player.GetComponent<PlayerSpit>();
            follower._renderer = go.GetComponent<SpriteRenderer>();
            follower._baptized = baptized;
            follower._phase = Active.Count * 1.7f;
            follower.LoadSprites();
            go.transform.position = player.position + Vector3.left * (1f + Active.Count * 0.35f);
            Active.Add(follower);
            return follower;
        }

        public static int BaptizeNext()
        {
            foreach (BelieverFollower follower in Active)
            {
                if (follower == null || follower._baptized) continue;
                follower._baptized = true;
                follower.LoadSprites();
                return 1;
            }
            return 0;
        }

        private void LoadSprites()
        {
            if (_baptized)
            {
                _front = Catalog != null ? Catalog.darkBelieverFront : null;
                _side = Catalog != null ? Catalog.darkBelieverSide : null;
                _back = Catalog != null ? Catalog.darkBelieverBack : null;
            }
            else
            {
                _front = Catalog != null ? Catalog.believerFront : null;
                _side = Catalog != null ? Catalog.believerSide : null;
                _back = Catalog != null ? Catalog.believerBack : null;
            }
            if (_renderer != null) _renderer.sprite = _front;
        }

        private void Update()
        {
            if (_player == null) { Destroy(gameObject); return; }
            PlayerController controller = _player.GetComponent<PlayerController>();
            Vector2 facing = controller != null ? controller.FacingDirection : Vector2.down;
            EnemyBase target = FindFrontEnemy(facing);
            Vector2 desired = _baptized && target != null
                ? (Vector2)target.transform.position
                : (Vector2)_player.position - facing * (1.25f + Active.IndexOf(this) * 0.25f);
            transform.position = Vector2.Lerp(transform.position, desired, 1f - Mathf.Exp(-8f * Time.deltaTime));
            transform.position += Vector3.up * (Mathf.Sin(Time.time * 2.2f + _phase) * 0.08f);
            if (target != null && Time.time >= _nextAttack)
            {
                _spit?.FireFollowerBall(transform.position, (target.transform.position - transform.position).normalized,
                    0.3f * (1f + (RogueSkillManager.Active?.GetLevel(RogueSkillId.PopePray) ?? 0) * 0.1f));
                float interval = 1.5f - (RogueSkillManager.Active?.GetLevel(RogueSkillId.PopeBelief) ?? 0) * 0.1f;
                _nextAttack = Time.time + Mathf.Max(0.2f, interval);
            }
            if (_renderer != null)
            {
                _renderer.sprite = controller == null ? _front : controller.CurrentFacing switch
                { Facing.Back => _back, Facing.Front => _front, _ => _side };
                _renderer.flipX = controller != null && controller.CurrentFacing == Facing.SideRight;
            }
        }

        private EnemyBase FindFrontEnemy(Vector2 facing)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll((Vector2)_player.position + facing * 2f, 4f);
            EnemyBase best = null;
            float bestDot = 0.35f;
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit != null ? hit.GetComponentInParent<EnemyBase>() : null;
                if (enemy == null || enemy.IsDead) continue;
                float dot = Vector2.Dot(facing, ((Vector2)enemy.transform.position - (Vector2)_player.position).normalized);
                if (dot > bestDot) { bestDot = dot; best = enemy; }
            }
            return best;
        }

        private void OnDestroy() => Active.Remove(this);
    }
}
