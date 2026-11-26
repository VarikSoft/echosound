using UnityEngine;
using EchoSound.Combat;

namespace EchoSound.UI
{
    /// <summary>
    /// Minimalist HUD displaying the player's Health and Stamina bars,
    /// with visual feedback for damage and exhaustion.
    /// </summary>
    public sealed class PlayerStatsHUD : MonoBehaviour
    {
        [Header("Target References (Optional, auto-detected if null)")]
        public HealthComponent health;
        public StaminaComponent stamina;

        [Header("HUD Configuration")]
        public bool showHUD = true;
        public float barWidth = 180f;
        public float barHeight = 12f;
        public float padding = 16f;

        private Texture2D bgTexture;
        private Texture2D healthTexture;
        private Texture2D staminaTexture;
        private Texture2D exhaustedTexture;
        private Texture2D damageFlashTexture;
        private GUIStyle labelStyle;
        private GUIStyle deathStyle;
        private float damageFlashAlpha;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<HealthComponent>();
            if (stamina == null) stamina = GetComponentInParent<StaminaComponent>();
            InitTextures();
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponentInParent<HealthComponent>();
            if (health != null) health.OnDamageTaken += HandleDamageTaken;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDamageTaken -= HandleDamageTaken;
        }

        private void HandleDamageTaken(DamageInfo _)
        {
            damageFlashAlpha = 0.45f;
        }

        private void Update()
        {
            if (damageFlashAlpha > 0f)
            {
                damageFlashAlpha = Mathf.Max(0f, damageFlashAlpha - Time.deltaTime * 1.4f);
            }
        }

        private void InitTextures()
        {
            bgTexture = CreateSolidTexture(new Color(0.1f, 0.12f, 0.15f, 0.65f));
            healthTexture = CreateSolidTexture(new Color(0.18f, 0.85f, 0.55f, 0.9f));
            staminaTexture = CreateSolidTexture(new Color(0.95f, 0.75f, 0.15f, 0.9f));
            exhaustedTexture = CreateSolidTexture(new Color(0.9f, 0.25f, 0.2f, 0.9f));
            damageFlashTexture = CreateSolidTexture(new Color(0.85f, 0.08f, 0.08f, 1f));
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (!showHUD || Event.current.type != EventType.Repaint) return;

            if (health == null)
            {
                health = GetComponentInParent<HealthComponent>();
                if (health != null) health.OnDamageTaken += HandleDamageTaken;
            }
            if (stamina == null) stamina = GetComponentInParent<StaminaComponent>();

            if (damageFlashAlpha > 0.01f && damageFlashTexture != null)
            {
                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, damageFlashAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), damageFlashTexture);
                GUI.color = prevColor;
            }

            if (health != null && !health.IsAlive)
            {
                if (deathStyle == null)
                {
                    deathStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 36,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    deathStyle.normal.textColor = new Color(0.95f, 0.2f, 0.2f, 1f);
                }
                GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 60f), "YOU DIED", deathStyle);
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            float startX = padding;
            float startY = Screen.height - (barHeight * 2 + 35f + padding);

            // 1. Health Bar
            if (health != null)
            {
                Rect hpBg = new Rect(startX, startY, barWidth, barHeight);
                GUI.DrawTexture(hpBg, bgTexture);

                float hpPercent = health.HealthPercent;
                Rect hpFill = new Rect(startX, startY, barWidth * hpPercent, barHeight);
                GUI.DrawTexture(hpFill, healthTexture);

                labelStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(startX + barWidth + 8f, startY - 2f, 100f, barHeight + 4f),
                    $"{(int)health.CurrentHealth}/{(int)health.MaxHealth}", labelStyle);
            }

            // 2. Stamina Bar
            if (stamina != null)
            {
                float stY = startY + barHeight + 6f;
                Rect stBg = new Rect(startX, stY, barWidth, barHeight);
                GUI.DrawTexture(stBg, bgTexture);

                float stPercent = stamina.StaminaPercent;
                bool isExhausted = stamina.IsExhausted;
                Texture2D stTex = isExhausted ? exhaustedTexture : staminaTexture;

                Rect stFill = new Rect(startX, stY, barWidth * stPercent, barHeight);
                GUI.DrawTexture(stFill, stTex);

                labelStyle.normal.textColor = isExhausted ? new Color(1f, 0.4f, 0.3f) : new Color(0.95f, 0.85f, 0.5f);
                string stText = isExhausted ? "EXHAUSTED" : $"{(int)stamina.CurrentStamina}/{(int)stamina.MaxStamina}";
                GUI.Label(new Rect(startX + barWidth + 8f, stY - 2f, 120f, barHeight + 4f), stText, labelStyle);
            }
        }
    }
}
