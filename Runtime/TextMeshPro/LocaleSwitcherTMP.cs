using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LocalizedDomain.Unity
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public sealed class LocaleSwitcherTMP : LocaleSwitcherBase
    {
        [SerializeField] private TMP_Dropdown _dropdown;

        private void Awake()
        {
            if (_dropdown == null)
            {
                _dropdown = GetComponent<TMP_Dropdown>();
            }

            if (_dropdown == null)
            {
                throw new NullReferenceException("LocaleSwitcherTMP requires a TMP_Dropdown reference.");
            }
        }

        protected override void ApplyOptions(List<string> labels)
        {
            _dropdown.ClearOptions();
            _dropdown.AddOptions(labels);
        }

        protected override void SetSelectedIndex(int index)
        {
            if (_dropdown.options.Count == 0)
            {
                return;
            }

            var clamped = Mathf.Clamp(index, 0, _dropdown.options.Count - 1);
            _dropdown.SetValueWithoutNotify(clamped);
        }

        protected override void Subscribe()
        {
            _dropdown.onValueChanged.AddListener(SetLocaleByIndex);
        }

        protected override void Unsubscribe()
        {
            _dropdown.onValueChanged.RemoveListener(SetLocaleByIndex);
        }
    }
}
