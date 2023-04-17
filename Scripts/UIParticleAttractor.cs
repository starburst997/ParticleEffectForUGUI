using UnityEngine;
using Coffee.UIParticleExtensions;
using UnityEngine.Events;
using System;

namespace Coffee.UIExtensions
{
    [ExecuteAlways]
    public class UIParticleAttractor : MonoBehaviour
    {
        public enum Movement
        {
            Linear,
            Smooth,
            Sphere,
        }

        public bool Fade = true;
        public float MaxDistance = 0.25f;
        public Vector3 DestinationOffset;
        
        [SerializeField]
        private ParticleSystem m_ParticleSystem;

        [Range(0.1f, 10f)]
        [SerializeField]
        private float m_DestinationRadius = 1;

        [Range(0f, 0.95f)]
        [SerializeField]
        private float m_DelayRate = 0;

        [Range(0.001f, 100f)]
        [SerializeField]
        private float m_MaxSpeed = 1;

        [SerializeField]
        private Movement m_Movement;

        [SerializeField]
        private UnityEvent<bool> m_OnAttracted;

        public float delay
        {
            get
            {
                return m_DelayRate;
            }
            set
            {
                m_DelayRate = value;
            }
        }

        public float maxSpeed
        {
            get
            {
                return m_MaxSpeed;
            }
            set
            {
                m_MaxSpeed = value;
            }
        }

        public Movement movement
        {
            get
            {
                return m_Movement;
            }
            set
            {
                m_Movement = value;
            }
        }
        
        public ParticleSystem particleSystem
        {
            get
            {
                return m_ParticleSystem;
            }
            set
            {
                m_ParticleSystem = value;
                if (!ApplyParticleSystem()) return;
                enabled = true;
            }
        }

        private UIParticle _uiParticle;
        private float _delayDeactivate;

        private bool ApplyParticleSystem()
        {
            if (m_ParticleSystem == null)
            {
                //Debug.LogError("No particle system attached to particle attractor script", this);
                enabled = false;
                return false;
            }

            _uiParticle = m_ParticleSystem.GetComponentInParent<UIParticle>();
            if (_uiParticle && !_uiParticle.particles.Contains(m_ParticleSystem))
            {
                _uiParticle = null;
            }

            return true;
        }
        
        private void OnEnable()
        {
            if (!ApplyParticleSystem()) return;
            UIParticleUpdater.Register(this);
        }

        private void OnDisable()
        {
            _uiParticle = null;
            UIParticleUpdater.Unregister(this);
        }
        
        internal void Attract()
        {
            if (m_ParticleSystem == null) return;
            
            var count = m_ParticleSystem.particleCount;
            if (count == 0) return;

            var particles = ParticleSystemExtensions.GetParticleArray(count);
            m_ParticleSystem.GetParticles(particles, count);
            
            var dstPos = GetDestinationPosition() + DestinationOffset;
            for (var i = 0; i < count; i++)
            {
                // Attracted
                var p = particles[i];
                var distance = Vector3.Distance(p.position, dstPos);
                if (0f < p.remainingLifetime && distance < m_DestinationRadius)
                {
                    p.remainingLifetime = 0f;
                    particles[i] = p;
                    
                    m_OnAttracted?.Invoke(count <= 1);
                    continue;
                }

                // Calc attracting time
                var delayTime = p.startLifetime * m_DelayRate;
                var duration = p.startLifetime - delayTime;
                var time = Mathf.Max(0, p.startLifetime - p.remainingLifetime - delayTime);

                // Delay
                if (time <= 0) continue;

                // Attract
                p.position = GetAttractedPosition(p.position, dstPos, duration, time);
                
                //p.velocity *= 0.5f;
                
                //if (distance > 4f) p.velocity = dstPos - p.position;
                
                // When close to the destination, fade color / scale
                if (Fade && distance < MaxDistance)
                {
                    var perc = distance / MaxDistance;
                    p.startColor = new Color32(p.startColor.r, p.startColor.g, p.startColor.b, (byte) (perc * 0xFF));
                    p.startSize = perc;
                }

                particles[i] = p;
            }

            m_ParticleSystem.SetParticles(particles, count);
        }

        private Vector3 GetDestinationPosition()
        {
            var isUI = _uiParticle && _uiParticle.enabled;
            var psPos = m_ParticleSystem.transform.position;
            var attractorPos = transform.position;
            
            var dstPos = attractorPos;
            if (m_ParticleSystem.main.simulationSpace == ParticleSystemSimulationSpace.Local)
            {
                dstPos = m_ParticleSystem.transform.InverseTransformPoint(dstPos);
                if (isUI)
                {
                    dstPos = dstPos.GetScaled(_uiParticle.transform.localScale, _uiParticle.scale3D.Inverse());
                }

                dstPos.z = 0;
            }
            else
            {
#if UNITY_EDITOR
                if (!Application.isPlaying && isUI)
                {
                    var diff = dstPos - psPos;
                    diff = diff.GetScaled(_uiParticle.transform.localScale, _uiParticle.scale3D.Inverse());
                    return psPos + diff;
                }
#endif
                if (isUI)
                {
                    dstPos.Scale(_uiParticle.transform.localScale);
                    dstPos.Scale(_uiParticle.scale3D.Inverse());
                }
            }
            return dstPos;
        }

        private Vector3 GetAttractedPosition(Vector3 current, Vector3 target, float duration, float time)
        {
            var speed = m_MaxSpeed;
            switch (m_Movement)
            {
                case Movement.Linear:
                    speed /= duration;
                    break;
                case Movement.Smooth:
                    target = Vector3.Lerp(current, target, time / duration);
                    break;
                case Movement.Sphere:
                    target = Vector3.Slerp(current, target, time / duration);
                    break;
            }
            
            return Vector3.MoveTowards(current, target, speed);
        }

    }
}