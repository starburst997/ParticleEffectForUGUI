﻿using System;
using Coffee.UIParticleExtensions;
using UnityEngine;
using UnityEngine.Events;

namespace Coffee.UIExtensions
{
    [ExecuteAlways]
    public class UIParticleAttractor : MonoBehaviour
    {
        public enum Movement
        {
            Linear,
            Smooth,
            Sphere
        }

        public enum UpdateMode
        {
            Normal,
            UnscaledTime
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
        private float m_DelayRate;

        [Range(0.001f, 100f)]
        [SerializeField]
        private float m_MaxSpeed = 1;

        [SerializeField]
        private Movement m_Movement;

        [SerializeField]
        private UpdateMode m_UpdateMode;

        [SerializeField]
        private UnityEvent<bool> m_OnAttracted;

        private UIParticle _uiParticle;
        private float _delayDeactivate;

        public float destinationRadius
        {
            get { return m_DestinationRadius; }
            set { m_DestinationRadius = Mathf.Clamp(value, 0.1f, 10f); }
        }

        public float delay
        {
            get { return m_DelayRate; }
            set { m_DelayRate = value; }
        }

        public float maxSpeed
        {
            get { return m_MaxSpeed; }
            set { m_MaxSpeed = value; }
        }

        public Movement movement
        {
            get { return m_Movement; }
            set { m_Movement = value; }
        }

        public UpdateMode updateMode
        {
            get { return m_UpdateMode; }
            set { m_UpdateMode = value; }
        }

        public UnityEvent<bool> onAttracted
        {
            get { return m_OnAttracted; }
            set { m_OnAttracted = value; }
        }

#if UNITY_EDITOR
        public new ParticleSystem particleSystem
#else
        public ParticleSystem particleSystem
#endif
        {
            get { return m_ParticleSystem; }
            set
            {
                m_ParticleSystem = value;
                if (!ApplyParticleSystem()) return;
                enabled = true;
            }
        }

        private void OnEnable()
        {
            if (!ApplyParticleSystem()) return;
            UIParticleUpdater.Register(this);
        }

        private void OnDisable()
        {
            UIParticleUpdater.Unregister(this);
        }

        private void OnDestroy()
        {
            _uiParticle = null;
            m_ParticleSystem = null;
        }
        
        public void KillAll()
        {
            if (m_ParticleSystem == null) return;

            var count = m_ParticleSystem.particleCount;
            if (count == 0) return;

            var particles = ParticleSystemExtensions.GetParticleArray(count);
            m_ParticleSystem.GetParticles(particles, count);

            var totalCount = count;
            for (var i = 0; i < count; i++)
            {
                var p = particles[i];
                p.remainingLifetime = 0f;
                particles[i] = p;

                m_OnAttracted?.Invoke(totalCount-- <= 1);
            }

            m_ParticleSystem.SetParticles(particles, count);
        }

        internal void Attract()
        {
            if (m_ParticleSystem == null) return;

            var count = m_ParticleSystem.particleCount;
            if (count == 0) return;

            var particles = ParticleSystemExtensions.GetParticleArray(count);
            m_ParticleSystem.GetParticles(particles, count);

            var totalCount = count;
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

                    m_OnAttracted?.Invoke(totalCount-- <= 1);
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
            var isLocalSpace = m_ParticleSystem.IsLocalSpace();

            if (isLocalSpace)
            {
                dstPos = m_ParticleSystem.transform.InverseTransformPoint(dstPos);
            }

            if (isUI)
            {
                var inverseScale = _uiParticle.parentScale.Inverse();
                dstPos = dstPos.GetScaled(inverseScale, _uiParticle.scale3D.Inverse());
                dstPos.z = 0;
                
                // Relative mode
                if (_uiParticle.positionMode == UIParticle.PositionMode.Relative)
                {
                    var diff = _uiParticle.transform.position - psPos;
                    diff.Scale(_uiParticle.scale3D - inverseScale);
                    diff.Scale(_uiParticle.scale3D.Inverse());
                    dstPos += diff;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying && !isLocalSpace)
                {
                    dstPos += psPos - psPos.GetScaled(inverseScale, _uiParticle.scale3D.Inverse());
                }
#endif
            }

            return dstPos;
        }

        private Vector3 GetAttractedPosition(Vector3 current, Vector3 target, float duration, float time)
        {
            var speed = m_MaxSpeed;
            switch (m_UpdateMode)
            {
                case UpdateMode.Normal:
                    speed *= 60 * Time.deltaTime;
                    break;
                case UpdateMode.UnscaledTime:
                    speed *= 60 * Time.unscaledDeltaTime;
                    break;
            }

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

        private bool ApplyParticleSystem()
        {
            _uiParticle = null;
            if (m_ParticleSystem == null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
#endif
                {
                    //Debug.LogError("No particle system attached to particle attractor script", this);
                }

                return false;
            }

            _uiParticle = (UIParticle) m_ParticleSystem.gameObject.GetComponentInParent(typeof(UIParticle), true);
            if (_uiParticle && !_uiParticle.particles.Contains(m_ParticleSystem))
            {
                _uiParticle = null;
            }
            
            return true;
        }
    }
}
