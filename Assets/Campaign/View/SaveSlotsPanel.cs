using System;
using Century.Core;
using Century.Core.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Campaign.View
{
    /// <summary>
    /// The three save slots, as an overlay over whichever document declares the <c>save-modal</c>
    /// block (the title screen and the overmap HUD both do, with identical element names). Works
    /// in two modes: LOAD from the title, SAVE from the march. Talks only to
    /// <see cref="ISaveSlots"/>; where the files live is the App layer's business.
    /// </summary>
    public sealed class SaveSlotsPanel
    {
        public enum Mode { Load, Save }

        private const int SlotCount = 3;

        private readonly VisualElement _modal;
        private readonly Label _title, _subtitle, _status, _actionLabel;
        private readonly Button _action, _delete, _close;
        private readonly Button[] _slots = new Button[SlotCount];
        private readonly Label[] _slotTitles = new Label[SlotCount];
        private readonly Label[] _slotDetails = new Label[SlotCount];
        private readonly Label[] _slotDates = new Label[SlotCount];

        private Mode _mode;
        private int _selected = -1;

        public bool IsOpen => _modal != null && _modal.style.display == DisplayStyle.Flex;

        /// <summary>The X, or a completed action, closed the overlay.</summary>
        public event Action Closed;

        public SaveSlotsPanel(VisualElement root)
        {
            if (root == null) return;

            _modal = root.Q<VisualElement>("save-modal");
            _title = root.Q<Label>("save-title");
            _subtitle = root.Q<Label>("save-subtitle");
            _status = root.Q<Label>("save-status");
            _action = root.Q<Button>("save-action");
            _actionLabel = root.Q<Label>("save-action-label");
            _delete = root.Q<Button>("save-delete");
            _close = root.Q<Button>("save-close");

            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = root.Q<Button>($"save-slot-{i}");
                _slotTitles[i] = root.Q<Label>($"save-slot-title-{i}");
                _slotDetails[i] = root.Q<Label>($"save-slot-detail-{i}");
                _slotDates[i] = root.Q<Label>($"save-slot-date-{i}");

                if (_slots[i] == null) continue;
                int slot = i;
                _slots[i].clicked += () => Select(slot);
            }

            if (_action != null) _action.clicked += Act;
            if (_delete != null) _delete.clicked += DeleteSelected;
            if (_close != null) _close.clicked += Close;

            if (_modal != null) _modal.style.display = DisplayStyle.None;
        }

        public void Open(Mode mode)
        {
            if (_modal == null) return;

            _mode = mode;
            _selected = -1;
            SetText(_status, string.Empty);
            SetText(_title, mode == Mode.Load ? "LOAD SAVE" : "SAVE GAME");
            SetText(_subtitle, mode == Mode.Load
                ? "Choose a slot, then LOAD"
                : "Choose a slot, then SAVE. A held slot is overwritten");
            SetText(_actionLabel, mode == Mode.Load ? "LOAD" : "SAVE");

            RefreshSlots();
            _modal.style.display = DisplayStyle.Flex;
        }

        public void Close()
        {
            if (_modal == null) return;
            _modal.style.display = DisplayStyle.None;
            Closed?.Invoke();
        }

        private void Select(int slot)
        {
            _selected = slot;
            SetText(_status, string.Empty);
            RefreshSlots();
        }

        private void RefreshSlots()
        {
            ServiceLocator.TryGet(out ISaveSlots saves);

            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null) continue;

                SaveSlotInfo info = saves != null
                    ? saves.Describe(i)
                    : new SaveSlotInfo { Slot = i, IsEmpty = true };

                _slots[i].EnableInClassList("save-slot--empty", info.IsEmpty);
                _slots[i].EnableInClassList("save-slot--selected", i == _selected);

                SetText(_slotTitles[i], info.IsEmpty ? "EMPTY SLOT" : info.Title);
                SetText(_slotDetails[i], info.IsEmpty
                    ? (_mode == Mode.Save ? "Nothing saved here yet" : "Nothing to load")
                    : info.Detail);
                SetText(_slotDates[i], info.IsEmpty ? string.Empty : info.SavedAt);
            }

            bool selectedHeld = _selected >= 0 && saves != null && !saves.Describe(_selected).IsEmpty;
            bool canAct = _selected >= 0 && (_mode == Mode.Save ? saves != null && saves.CanSaveNow : selectedHeld);

            _action?.SetEnabled(canAct);
            _delete?.SetEnabled(selectedHeld);
            if (_delete != null) _delete.style.display = selectedHeld ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Act()
        {
            if (_selected < 0 || !ServiceLocator.TryGet(out ISaveSlots saves)) return;

            if (_mode == Mode.Save)
            {
                bool ok = saves.Save(_selected);
                SetText(_status, ok ? $"Saved to slot {_selected + 1}." : "The save could not be written.");
                RefreshSlots();
                return;
            }

            // Loading swaps the whole campaign and the scene; the overlay is gone with it.
            if (!saves.Load(_selected))
            {
                SetText(_status, "That slot could not be read.");
                return;
            }

            Close();
        }

        private void DeleteSelected()
        {
            if (_selected < 0 || !ServiceLocator.TryGet(out ISaveSlots saves)) return;
            saves.Delete(_selected);
            SetText(_status, $"Slot {_selected + 1} cleared.");
            RefreshSlots();
        }

        private static void SetText(Label label, string text)
        {
            if (label != null) label.text = text ?? string.Empty;
        }
    }
}
