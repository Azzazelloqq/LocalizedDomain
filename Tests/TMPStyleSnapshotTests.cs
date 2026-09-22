#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using LocalizedDomain.Unity;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace LocalizedDomain.Tests
{
    public sealed class TMPStyleSnapshotTests
    {
        private GameObject _owner;
        private TMP_Text _text;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("TMP snapshot test");
            _owner.SetActive(false);
            _text = _owner.AddComponent<TextMeshPro>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_owner);

        [TestCase(TextWrappingModes.Normal, true)]
        [TestCase(TextWrappingModes.PreserveWhitespace, true)]
        [TestCase(TextWrappingModes.NoWrap, false)]
        [TestCase(TextWrappingModes.PreserveWhitespaceNoWrap, false)]
        public void Capture_RecordsWhetherWrappingIsEnabled(TextWrappingModes mode, bool expected)
        {
            _text.textWrappingMode = mode;
            var snapshot = new TMPStyleSnapshot();
            snapshot.Capture(_text);
            Assert.That(snapshot.WordWrapping, Is.EqualTo(expected));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CaptureAndApply_RestoresWrappingAndKerningWithoutRemovingOtherFeatures(bool enabled)
        {
            _text.textWrappingMode = enabled ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            _text.fontFeatures = enabled
                ? new List<OTL_FeatureTag> { OTL_FeatureTag.kern }
                : new List<OTL_FeatureTag>();
            var snapshot = new TMPStyleSnapshot();
            snapshot.Capture(_text);
            Assert.That(snapshot.EnableKerning, Is.EqualTo(enabled));

            _text.textWrappingMode = enabled ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
            _text.fontFeatures = new List<OTL_FeatureTag> { OTL_FeatureTag.liga, OTL_FeatureTag.kern };
            snapshot.Apply(_text);
            snapshot.Apply(_text);

            Assert.That(_text.textWrappingMode, Is.EqualTo(enabled ? TextWrappingModes.Normal : TextWrappingModes.NoWrap));
            Assert.That(_text.fontFeatures, Is.EquivalentTo(enabled
                ? new[] { OTL_FeatureTag.liga, OTL_FeatureTag.kern }
                : new[] { OTL_FeatureTag.liga }));
        }
    }
}
#endif
