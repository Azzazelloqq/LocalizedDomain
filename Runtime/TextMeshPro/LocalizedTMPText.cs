using System;
using TMPro;
using UnityEngine;

namespace LocalizedDomain.Unity
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedTMPText : LocalizedTextBase
    {
        [SerializeField] private TMPStyleSnapshot _snapshot = new();
        [SerializeField] private bool _applySnapshotOnEnable = true;

        private TMP_Text _text;

        protected override void OnEnable()
        {
            Cache();
            if (_applySnapshotOnEnable)
            {
                _snapshot.Apply(_text);
            }
            base.OnEnable();
        }

        private void Reset()
        {
            Cache();
            _snapshot.Capture(_text);
        }

        protected override void OnValidate()
        {
            Cache();
            if (!Application.isPlaying)
            {
                _snapshot.Capture(_text);
            }

            base.OnValidate();
        }

        protected override void ApplyText(string text)
        {
            if (_text == null)
            {
                Cache();
            }

            if (_text != null)
            {
                _text.text = text ?? string.Empty;
            }
        }

        private void Cache()
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }
        }
    }

    [Serializable]
    public sealed class TMPStyleSnapshot
    {
        public TMP_FontAsset Font;
        public Material FontMaterial;
        public float FontSize;
        public FontStyles FontStyle;
        public TextAlignmentOptions Alignment;
        public Color Color;
        public bool RichText;
        public bool WordWrapping;
        public TextOverflowModes OverflowMode;
        public bool AutoSize;
        public float FontSizeMin;
        public float FontSizeMax;
        public float LineSpacing;
        public float CharacterSpacing;
        public float ParagraphSpacing;
        public float WordSpacing;
        public bool EnableKerning;

        public void Capture(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            Font = text.font;
            FontMaterial = text.fontSharedMaterial;
            FontSize = text.fontSize;
            FontStyle = text.fontStyle;
            Alignment = text.alignment;
            Color = text.color;
            RichText = text.richText;
            WordWrapping = text.enableWordWrapping;
            OverflowMode = text.overflowMode;
            AutoSize = text.enableAutoSizing;
            FontSizeMin = text.fontSizeMin;
            FontSizeMax = text.fontSizeMax;
            LineSpacing = text.lineSpacing;
            CharacterSpacing = text.characterSpacing;
            ParagraphSpacing = text.paragraphSpacing;
            WordSpacing = text.wordSpacing;
            EnableKerning = text.enableKerning;
        }

        public void Apply(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            if (Font != null)
            {
                text.font = Font;
            }

            if (FontMaterial != null)
            {
                text.fontSharedMaterial = FontMaterial;
            }

            text.fontSize = FontSize;
            text.fontStyle = FontStyle;
            text.alignment = Alignment;
            text.color = Color;
            text.richText = RichText;
            text.enableWordWrapping = WordWrapping;
            text.overflowMode = OverflowMode;
            text.enableAutoSizing = AutoSize;
            text.fontSizeMin = FontSizeMin;
            text.fontSizeMax = FontSizeMax;
            text.lineSpacing = LineSpacing;
            text.characterSpacing = CharacterSpacing;
            text.paragraphSpacing = ParagraphSpacing;
            text.wordSpacing = WordSpacing;
            text.enableKerning = EnableKerning;
        }
    }
}
