using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TankHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] float _maxHealth = 100f;
    [SerializeField] float _damagedHealthThreshold = 50f;

    [Header("Hit Reaction")]
    [SerializeField] Color _hitFlashColor = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] float _hitFlashDuration = 0.35f;
    [SerializeField] Color _damagedTint = new Color(0.45f, 0.4f, 0.38f, 1f);
    [SerializeField] Color _deadTint = new Color(0.18f, 0.16f, 0.15f, 1f);

    [Header("VFX")]
    [SerializeField] GameObject _damagedSmokePrefab;
    [SerializeField] GameObject _deathExplosionPrefab;
    [SerializeField] Vector3 _smokeLocalOffset = new Vector3(0f, 0f, 1.5f);
    [SerializeField] float _deathExplosionLifetime = 8f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    readonly List<Renderer> _bodyRenderers = new List<Renderer>();

    MaterialPropertyBlock _propertyBlock;
    GameObject _smokeInstance;
    Coroutine _flashRoutine;
    float _currentHealth;
    bool _isDamagedStateActive;
    bool _isDead;

    public event Action<TankHealth, float, float> HealthChanged;
    public event Action<TankHealth> Died;

    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsDead => _isDead;

    void Awake()
    {
        _currentHealth = _maxHealth;
        _propertyBlock = new MaterialPropertyBlock();
        CacheBodyRenderers();
    }

    void CacheBodyRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is LineRenderer || renderers[i] is ParticleSystemRenderer)
            {
                continue;
            }

            _bodyRenderers.Add(renderers[i]);
        }
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (_isDead || amount <= 0f)
        {
            return;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        HealthChanged?.Invoke(this, _currentHealth, _maxHealth);
        Debug.Log($"{name} took {amount} damage. HP: {_currentHealth}/{_maxHealth}", this);

        PlayHitFlash();

        if (_isDamagedStateActive == false && _currentHealth <= _damagedHealthThreshold)
        {
            EnterDamagedState();
        }

        if (_currentHealth <= 0f)
        {
            Die(hitPoint);
        }
    }

    void PlayHitFlash()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
        }

        _flashRoutine = StartCoroutine(HitFlashRoutine());
    }

    IEnumerator HitFlashRoutine()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _hitFlashDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Color color = Color.Lerp(_hitFlashColor, GetRestingTint(), progress);
            ApplyTint(color, true);
            yield return null;
        }

        ApplyRestingTint();
        _flashRoutine = null;
    }

    void EnterDamagedState()
    {
        _isDamagedStateActive = true;

        if (_damagedSmokePrefab != null && _smokeInstance == null)
        {
            _smokeInstance = Instantiate(_damagedSmokePrefab, transform);
            _smokeInstance.transform.localPosition = _smokeLocalOffset;
        }
    }

    void Die(Vector3 hitPoint)
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;

        if (_deathExplosionPrefab != null)
        {
            GameObject explosion = Instantiate(_deathExplosionPrefab, transform.position, Quaternion.identity);
            VfxUtility.MakeOneShot(explosion, _deathExplosionLifetime);
        }

        DisableControlBehaviours();
        ApplyRestingTint();
        Died?.Invoke(this);
        Debug.Log($"{name} destroyed.", this);
    }

    void DisableControlBehaviours()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != this)
            {
                behaviours[i].enabled = false;
            }
        }
    }

    Color GetRestingTint()
    {
        if (_isDead)
        {
            return _deadTint;
        }

        if (_isDamagedStateActive)
        {
            return _damagedTint;
        }

        return Color.white;
    }

    void ApplyRestingTint()
    {
        bool hasTint = _isDead || _isDamagedStateActive;
        ApplyTint(GetRestingTint(), hasTint);
    }

    void ApplyTint(Color color, bool useTint)
    {
        for (int i = 0; i < _bodyRenderers.Count; i++)
        {
            Renderer bodyRenderer = _bodyRenderers[i];
            if (bodyRenderer == null)
            {
                continue;
            }

            if (useTint == false)
            {
                bodyRenderer.SetPropertyBlock(null);
                continue;
            }

            _propertyBlock.Clear();
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            bodyRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
