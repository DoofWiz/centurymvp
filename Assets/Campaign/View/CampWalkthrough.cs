using System;
using System.Collections.Generic;
using Century.Campaign.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Campaign.View
{
    /// <summary>
    /// The decanus walks the player through the camp screen the first time they make camp in the
    /// Aftermath: what each part of the screen means, then two things they must actually do (appoint
    /// a Speculator, rest until dawn) before the walkthrough lets go. A coach panel pinned beside the
    /// part of the screen it is talking about, with that part lit.
    /// </summary>
    /// <remarks>
    /// Elements are re-queried by name every tick rather than cached: the organisation chart is
    /// rebuilt on every appointment, and a cached reference would light a dead element. The panel is
    /// declared last in the document so it paints over the pages, and is hidden while any modal is
    /// up so it never sits on top of the picker the step is asking the player to use.
    /// </remarks>
    public sealed class CampWalkthrough
    {
        private sealed class Step
        {
            public string Title;
            public string Body;
            /// <summary>Element to light and pin beside; null centres the panel.</summary>
            public string Target;
            /// <summary>Camp tab to show for the step, or -1 to leave the tab alone.</summary>
            public int Tab = -1;
            /// <summary>When set, NEXT is hidden until this returns true.</summary>
            public Func<bool> WaitFor;
            public string WaitText;
            public string NextLabel = "NEXT";
        }

        private const float PanelWidth = 380f;
        private const float PanelHeightGuess = 200f;

        private readonly VisualElement _root;
        private readonly CampaignState _state;
        private readonly Action<int> _selectTab;
        private readonly Func<bool> _anyModalOpen;

        private readonly VisualElement _panel;
        private readonly Label _stepLabel, _title, _body, _wait, _nextLabel;
        private readonly Button _next;

        private readonly List<Step> _steps = new List<Step>();
        private int _index = -1;
        private VisualElement _lit;
        private bool _rested;

        public bool IsActive { get; private set; }

        public CampWalkthrough(VisualElement root, CampaignState state, Action<int> selectTab, Func<bool> anyModalOpen)
        {
            _root = root;
            _state = state;
            _selectTab = selectTab;
            _anyModalOpen = anyModalOpen;

            _panel = root.Q<VisualElement>("coach-panel");
            _stepLabel = root.Q<Label>("coach-step");
            _title = root.Q<Label>("coach-title");
            _body = root.Q<Label>("coach-body");
            _wait = root.Q<Label>("coach-wait");
            _next = root.Q<Button>("coach-next");
            _nextLabel = root.Q<Label>("coach-next-label");

            if (_next != null) _next.clicked += Advance;

            BuildSteps();
        }

        private void BuildSteps()
        {
            PartyState party = _state.PlayerParty;

            _steps.Add(new Step
            {
                Title = "The camp",
                Body = "Valerius: \"This is where the century lives between marches, sir. Everything that is not " +
                       "marching or fighting happens here: resting, cooking, mending, appointing men to their offices. " +
                       "Let me show you round.\"",
                Tab = 0
            });

            _steps.Add(new Step
            {
                Title = "The strip",
                Body = "MEN is how many are alive. FOOD counts days before the men go hungry; FIREWOOD is the night " +
                       "fires; MEDICINE is doses for the wounded. COIN and DENARII buy what cannot be taken. " +
                       "The two dials are the state of the kit and the mood of the men.",
                Target = "top-bar",
                Tab = 0
            });

            _steps.Add(new Step
            {
                Title = "The pages",
                Body = "THE CENTURY is the chart of who does what. REST & ORDERS rests the men and sends them out. " +
                       "CRAFTING turns raw stores into food, medicine and kit. CAMP STATIONS raises the tents and " +
                       "fires that make all of it work better.",
                Target = "camp-tabs",
                Tab = 0
            });

            _steps.Add(new Step
            {
                Title = "The chart",
                Body = "The command posts stand at the top; the tent groups below. Click a man to read him. " +
                       "Click a post to appoint an officer to it, or to invest in the traditions of that office. " +
                       "An empty post is felt: the century is worse for it.",
                Target = "camp-page-0",
                Tab = 0
            });

            _steps.Add(new Step
            {
                Title = "Appoint a Speculator",
                Body = "Valerius: \"We are blind out here, sir. Give one of the men the Speculator's office: your lead " +
                       "scout. He ranges out from camp while the rest sleep and marks on the map what is worth marching " +
                       "to.\" Click the SPECULATOR post and choose a man.",
                Target = "officer-slot-Speculator",
                Tab = 0,
                WaitFor = () => party != null && party.Appointments.IsFilled(CampRole.Speculator),
                WaitText = "Waiting: appoint a man to the Speculator's post"
            });

            _steps.Add(new Step
            {
                Title = "Rest until dawn",
                Body = "Valerius: \"Now let them sleep, sir. The scout rides while the men rest and reports at first " +
                       "light. A night by the fire heals wounds, steadies nerves and mends kit; a cold camp does none of " +
                       "it.\" Press UNTIL DAWN.",
                Target = "rest-dawn",
                Tab = 1,
                WaitFor = () => _rested,
                WaitText = "Waiting: rest the men until dawn"
            });

            _steps.Add(new Step
            {
                Title = "Break camp",
                Body = "Valerius: \"The scout's found us a way out, sir: the passage north-west. When you are ready, " +
                       "BREAK CAMP and we march. Mind the looters between here and there.\"",
                Target = "break-camp",
                Tab = 1,
                NextLabel = "UNDERSTOOD"
            });
        }

        public void Begin()
        {
            if (_panel == null || _steps.Count == 0) return;
            IsActive = true;
            _index = -1;
            Advance();
        }

        /// <summary>The camp screen reports a completed rest, which the rest step waits on.</summary>
        public void OnRested() => _rested = true;

        private void Advance()
        {
            if (!IsActive) return;

            Unlight();
            _index++;

            if (_index >= _steps.Count)
            {
                Finish();
                return;
            }

            Step step = _steps[_index];
            if (step.Tab >= 0) _selectTab?.Invoke(step.Tab);

            SetText(_stepLabel, $"THE DECANUS · {_index + 1} / {_steps.Count}");
            SetText(_title, step.Title.ToUpperInvariant());
            SetText(_body, step.Body);
            SetText(_nextLabel, step.NextLabel);
            SetText(_wait, step.WaitText ?? string.Empty);

            _panel.style.display = DisplayStyle.Flex;
        }

        private void Finish()
        {
            IsActive = false;
            Unlight();
            if (_panel != null) _panel.style.display = DisplayStyle.None;
            if (_state?.Onboarding != null) _state.Onboarding.CampWalkthroughDone = true;
        }

        /// <summary>Every frame: keep the lit element lit, the panel pinned, and NEXT gated.</summary>
        public void Tick()
        {
            if (!IsActive || _panel == null || _index < 0 || _index >= _steps.Count) return;

            Step step = _steps[_index];

            // Never over a modal: the picker the step is asking for must be usable.
            bool modal = _anyModalOpen != null && _anyModalOpen();
            _panel.style.display = modal ? DisplayStyle.None : DisplayStyle.Flex;

            bool waiting = step.WaitFor != null && !step.WaitFor();
            if (_next != null) _next.style.display = waiting ? DisplayStyle.None : DisplayStyle.Flex;
            if (_wait != null) _wait.style.display = waiting ? DisplayStyle.Flex : DisplayStyle.None;

            VisualElement target = string.IsNullOrEmpty(step.Target) ? null : _root.Q<VisualElement>(step.Target);
            Light(target);
            Pin(target);
        }

        private void Light(VisualElement target)
        {
            if (_lit == target) return;
            Unlight();
            _lit = target;
            _lit?.AddToClassList("coach-target");
        }

        private void Unlight()
        {
            _lit?.RemoveFromClassList("coach-target");
            _lit = null;
        }

        /// <summary>Beside the target: below it by preference, above when there is no room, and
        /// never off the right edge. No target pins the panel to the centre of the screen.</summary>
        private void Pin(VisualElement target)
        {
            Rect screen = _root.worldBound;
            float width = Mathf.Min(PanelWidth, Mathf.Max(200f, screen.width - 40f));
            _panel.style.width = width;

            if (target == null || float.IsNaN(target.worldBound.width))
            {
                _panel.style.left = Mathf.Max(20f, (screen.width - width) * 0.5f);
                _panel.style.top = Mathf.Max(20f, screen.height * 0.5f - PanelHeightGuess * 0.5f);
                return;
            }

            Rect bound = target.worldBound;
            float height = _panel.resolvedStyle.height > 1f ? _panel.resolvedStyle.height : PanelHeightGuess;

            float left = bound.xMin;
            if (left + width > screen.width - 20f) left = screen.width - 20f - width;
            left = Mathf.Max(20f, left);

            float top = bound.yMax + 12f;
            if (top + height > screen.height - 20f) top = bound.yMin - 12f - height;
            if (top < 20f) top = Mathf.Max(20f, screen.height - 20f - height);

            _panel.style.left = left;
            _panel.style.top = top;
        }

        private static void SetText(Label label, string text)
        {
            if (label != null) label.text = text ?? string.Empty;
        }
    }
}
