using UnityEngine;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class InhaleAirEffect : MonoBehaviour
    {
        private PlayerInhale _inhale;
        private ParticleSystem _particles;
        private ParticleSystem.MainModule _main;
        private ParticleSystem.ShapeModule _shape;

        private void Awake()
        {
            _inhale = GetComponent<PlayerInhale>();
            GameObject child = new("InhaleAirParticles");
            child.transform.SetParent(transform, false);
            _particles = child.AddComponent<ParticleSystem>();
            _main = _particles.main;
            _main.loop = true;
            _main.playOnAwake = false;
            _main.startLifetime = 0.55f;
            _main.startSpeed = 3f;
            _main.startSize = 0.06f;
            _main.maxParticles = 64;
            _shape = _particles.shape;
            _shape.shapeType = ParticleSystemShapeType.Cone;
            _particles.GetComponent<ParticleSystemRenderer>().sortingOrder = 95;
        }

        private void Update()
        {
            if (_inhale == null || !_inhale.IsInhaling)
            {
                if (_particles.isPlaying) _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }
            _shape.angle = _inhale.CurrentInhaleAngle * 0.5f;
            _shape.radius = Mathf.Max(0.1f, _inhale.CurrentInhaleRadius * 0.3f);
            _shape.position = Vector3.zero;
            _main.startColor = RogueSkillManager.Active != null && RogueSkillManager.Active.Has(RogueSkillId.FaithDemon)
                ? new Color(0.03f, 0.03f, 0.03f, 0.9f) : new Color(0.7f, 0.9f, 1f, 0.8f);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_inhale.GetComponent<PlayerController>().FacingDirection.y, _inhale.GetComponent<PlayerController>().FacingDirection.x) * Mathf.Rad2Deg - 90f);
            if (_particles.isStopped) _particles.Play();
        }
    }
}
