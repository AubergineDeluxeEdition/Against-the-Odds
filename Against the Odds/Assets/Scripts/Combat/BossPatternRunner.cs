using System;
using UnityEngine;

public class BossPatternRunner
{
    private readonly EffectResolver resolver;
    private BossEncounterConfig encounter;
    private int currentPhaseIndex;
    private int actionIndex;
    private BossActionConfig chargingAction;
    private int remainingChargeTurns;
    private int phaseBossTurnsElapsed;
    private int phasePatternIterationsElapsed;

    public BossPhaseConfig CurrentPhase => HasConfiguredPhases ? encounter.phases[currentPhaseIndex] : null;
    private bool HasConfiguredPhases => encounter?.phases != null && encounter.phases.Length > 0;

    public event Action<string> OnBossDialogue;
    public event Action<string> OnBossAction;
    public event Action<AudioClip> OnBossAudio;
    public event Action<Sprite> OnBossSpriteChanged;
    public event Action<Sprite, float> OnBossVisualChanged;

    public BossPatternRunner(EffectResolver resolver)
    {
        this.resolver = resolver;
    }

    public void Initialize(BossEncounterConfig bossEncounter, CombatState state)
    {
        encounter = bossEncounter;
        currentPhaseIndex = 0;
        actionIndex = 0;
        chargingAction = null;
        remainingChargeTurns = 0;
        phaseBossTurnsElapsed = 0;
        phasePatternIterationsElapsed = 0;
        state.BossPhaseIndex = 0;
        state.BossHpDialoguePlayed = encounter.hpDialogues != null
            ? new bool[encounter.hpDialogues.Length]
            : Array.Empty<bool>();

        Say(encounter.introDialogue, encounter.introVoiceLine);

        SetBossVisual(encounter.bossSprite, encounter.bossVisualScale);

        if (HasConfiguredPhases)
        {
            EnterPhase(0, state);
        }
    }

    public void CheckHpDialogues(CombatState state)
    {
        if (encounter?.hpDialogues == null || state.BossHpDialoguePlayed == null) return;

        float hpPercent = state.EnemyMaxHP > 0 ? (float)state.EnemyHP / state.EnemyMaxHP : 0f;
        for (int i = 0; i < encounter.hpDialogues.Length; i++)
        {
            if (state.BossHpDialoguePlayed[i]) continue;

            BossHpDialogueConfig dialogue = encounter.hpDialogues[i];
            if (hpPercent <= dialogue.triggerWhenHpPercentAtOrBelow)
            {
                state.BossHpDialoguePlayed[i] = true;
                Say(dialogue.line, dialogue.voiceLine);
            }
        }
    }

    public void SayDeathDialogue()
    {
        Say(encounter?.deathDialogue, encounter?.deathVoiceLine);
    }

    public bool TryAdvancePhase(CombatState state)
    {
        if (!HasConfiguredPhases) return false;
        if (currentPhaseIndex >= encounter.phases.Length - 1) return false;

        BossPhaseConfig currentPhase = encounter.phases[currentPhaseIndex];
        if (state.EnemyHP <= 0 && currentPhase.deathEndsCombatBeforeNextPhase) return false;

        BossPhaseConfig nextPhase = encounter.phases[currentPhaseIndex + 1];
        if (!ShouldAdvanceToNextPhase(currentPhase, nextPhase, state)) return false;

        EnterPhase(currentPhaseIndex + 1, state);
        return true;
    }

    public void ExecuteEnemyTurn(CombatState state)
    {
        if (state.EnemyStunnedTurns > 0)
        {
            state.EnemyStunnedTurns--;
            OnBossAction?.Invoke("Etourdi");
            RegisterBossTurnSpent();
            return;
        }

        if (!HasConfiguredPhases)
        {
            resolver.ResolveEnemyAttack(encounter.defaultAttackDamage, state);
            OnBossAction?.Invoke($"Attaque {encounter.defaultAttackDamage}");
            RegisterBossTurnSpent();
            return;
        }

        if (chargingAction != null)
        {
            ContinueChargeOrResolve(state);
            RegisterBossTurnSpent();
            return;
        }

        BossActionConfig action = GetNextAction();
        if (action == null)
        {
            OnBossAction?.Invoke("Attend");
            RegisterBossTurnSpent();
            return;
        }

        StartSequence(action, state);
        RegisterBossTurnSpent();
    }

    private void EnterPhase(int phaseIndex, CombatState state)
    {
        currentPhaseIndex = phaseIndex;
        actionIndex = 0;
        chargingAction = null;
        remainingChargeTurns = 0;
        phaseBossTurnsElapsed = 0;
        phasePatternIterationsElapsed = 0;
        state.BossPhaseIndex = phaseIndex;

        BossPhaseConfig phase = encounter.phases[currentPhaseIndex];
        if (phase.maxHpOverride > 0)
        {
            state.EnemyMaxHP = phase.maxHpOverride;
        }

        if (phase.healToHpPercentOnStart > 0f)
        {
            state.EnemyHP = Mathf.Max(1, Mathf.CeilToInt(state.EnemyMaxHP * phase.healToHpPercentOnStart));
        }
        else if (state.EnemyHP <= 0)
        {
            state.EnemyHP = 1;
        }

        state.EnemyShield = Mathf.Max(state.EnemyShield, phase.shieldOnStart);
        state.EnemyInvulnerableTurns = Mathf.Max(state.EnemyInvulnerableTurns, phase.invulnerableTurnsOnStart);

        if (phase.bossSprite != null || phase.bossVisualScale > 0f)
        {
            Sprite sprite = phase.bossSprite != null ? phase.bossSprite : encounter.bossSprite;
            float scale = phase.bossVisualScale > 0f ? phase.bossVisualScale : encounter.bossVisualScale;
            SetBossVisual(sprite, scale);
        }

        string phaseName = string.IsNullOrWhiteSpace(phase.displayName) ? phase.phaseId : phase.displayName;
        OnBossAction?.Invoke(phaseName);

        if (phaseIndex > 0 && phase.phaseStartSfx != null)
        {
            OnBossAudio?.Invoke(phase.phaseStartSfx);
        }

        Say(phase.dialogueOnStart, null);
    }

    private BossActionConfig GetNextAction()
    {
        BossPhaseConfig phase = CurrentPhase;
        if (phase?.actions == null || phase.actions.Length == 0) return null;

        BossActionConfig action = phase.actions[actionIndex % phase.actions.Length];
        actionIndex++;
        return action;
    }

    private bool ShouldAdvanceToNextPhase(BossPhaseConfig currentPhase, BossPhaseConfig nextPhase, CombatState state)
    {
        float hpPercent = state.EnemyMaxHP > 0 ? (float)state.EnemyHP / state.EnemyMaxHP : 0f;
        bool reachedHpThreshold = state.EnemyHP <= 0 || hpPercent <= nextPhase.startsWhenHpPercentAtOrBelow;
        bool reachedTurnDuration = currentPhase.advanceAfterBossTurns > 0
            && phaseBossTurnsElapsed >= currentPhase.advanceAfterBossTurns;
        bool reachedPatternIterations = currentPhase.advanceAfterPatternIterations > 0
            && phasePatternIterationsElapsed >= currentPhase.advanceAfterPatternIterations;

        return reachedHpThreshold || reachedTurnDuration || reachedPatternIterations;
    }

    private void RegisterBossTurnSpent()
    {
        if (!HasConfiguredPhases) return;

        phaseBossTurnsElapsed++;
    }

    private void RegisterResolvedAction(BossActionConfig action)
    {
        BossPhaseConfig phase = CurrentPhase;
        if (phase?.actions == null || phase.actions.Length == 0 || action == null) return;
        if (actionIndex <= 0 || actionIndex % phase.actions.Length != 0) return;

        phasePatternIterationsElapsed++;
    }

    private void StartSequence(BossActionConfig action, CombatState state)
    {
        Say(action.bossLine, action.bossVoiceLine);
        ResolveEffects(action.startEffects, state);

        if (action.chargeTurns > 0)
        {
            chargingAction = action;
            remainingChargeTurns = action.chargeTurns;
            ApplyChargeTurn(action, state);
            OnBossAction?.Invoke($"Charge {action.actionId}");
            return;
        }

        ResolveSequence(action, state);
    }

    private void ContinueChargeOrResolve(CombatState state)
    {
        if (TryInterruptChargingAction(state)) return;

        remainingChargeTurns--;
        if (remainingChargeTurns > 0)
        {
            ApplyChargeTurn(chargingAction, state);
            OnBossAction?.Invoke($"Charge {chargingAction.actionId}");
            return;
        }

        BossActionConfig action = chargingAction;
        chargingAction = null;
        ResolveSequence(action, state);
    }

    private void ApplyChargeTurn(BossActionConfig action, CombatState state)
    {
        Say(action.chargeLine, action.chargeVoiceLine);

        if (action.vulnerableWhileCharging)
        {
            state.EnemyVulnerableTurns = Mathf.Max(state.EnemyVulnerableTurns, 1);
        }

        ResolveEffects(action.chargeTurnEffects, state);
    }

    private bool TryInterruptChargingAction(CombatState state)
    {
        if (chargingAction == null) return false;
        if (chargingAction.interruptIfPlayerBlockAtLeast <= 0) return false;
        if (state.PlayerBlock < chargingAction.interruptIfPlayerBlockAtLeast) return false;

        Say(chargingAction.interruptLine, chargingAction.interruptVoiceLine);
        state.EnemyStunnedTurns = Mathf.Max(state.EnemyStunnedTurns, chargingAction.interruptBossStunTurns);
        chargingAction = null;
        remainingChargeTurns = 0;
        OnBossAction?.Invoke("Interrompu");
        return true;
    }

    private void ResolveSequence(BossActionConfig action, CombatState state)
    {
        bool hasResolveEffects = action.resolveEffects != null && action.resolveEffects.Length > 0;
        if (hasResolveEffects)
        {
            ResolveEffects(action.resolveEffects, state);
        }
        else
        {
            ResolveLegacyAction(action, state);
        }

        RegisterResolvedAction(action);
    }

    private void ResolveLegacyAction(BossActionConfig action, CombatState state)
    {
        switch (action.actionType)
        {
            case BossActionType.Attack:
                int damage = action.damage > 0 ? action.damage : encounter.defaultAttackDamage;
                resolver.ResolveEnemyAttack(damage, state);
                OnBossAction?.Invoke($"Attaque {damage}");
                break;

            case BossActionType.GainShield:
                state.EnemyShield += action.shield;
                OnBossAction?.Invoke($"Shield +{action.shield}");
                break;

            case BossActionType.BecomeInvulnerable:
                state.EnemyInvulnerableTurns = Mathf.Max(state.EnemyInvulnerableTurns, action.invulnerableTurns);
                OnBossAction?.Invoke($"Invulnerable {action.invulnerableTurns}");
                break;

            case BossActionType.BecomeVulnerable:
                state.EnemyVulnerableTurns = Mathf.Max(state.EnemyVulnerableTurns, action.vulnerableTurns);
                OnBossAction?.Invoke($"Vulnerable {action.vulnerableTurns}");
                break;

            case BossActionType.ClearInvulnerability:
                state.EnemyInvulnerableTurns = 0;
                OnBossAction?.Invoke("Invulnerabilite retiree");
                break;

            case BossActionType.Wait:
                OnBossAction?.Invoke("Attend");
                break;
        }
    }

    private void ResolveEffects(BossEffectConfig[] effects, CombatState state)
    {
        if (effects == null) return;

        foreach (BossEffectConfig effect in effects)
        {
            ResolveEffect(effect, state);
        }
    }

    private void ResolveEffect(BossEffectConfig effect, CombatState state)
    {
        Say(effect.line, effect.voiceLine);

        switch (effect.effectType)
        {
            case BossEffectType.AttackPlayer:
                resolver.ResolveEnemyAttack(effect.value, state);
                OnBossAction?.Invoke($"Attaque {effect.value}");
                break;

            case BossEffectType.GainShield:
                state.EnemyShield += effect.value;
                OnBossAction?.Invoke($"Shield +{effect.value}");
                break;

            case BossEffectType.LoseSelfHp:
                state.EnemyHP = Mathf.Max(0, state.EnemyHP - effect.value);
                OnBossAction?.Invoke($"Sacrifice {effect.value}");
                break;

            case BossEffectType.HealSelf:
                state.EnemyHP = Mathf.Min(state.EnemyMaxHP, state.EnemyHP + effect.value);
                OnBossAction?.Invoke($"Soin {effect.value}");
                break;

            case BossEffectType.BecomeInvulnerable:
                state.EnemyInvulnerableTurns = Mathf.Max(state.EnemyInvulnerableTurns, effect.value);
                OnBossAction?.Invoke($"Invulnerable {effect.value}");
                break;

            case BossEffectType.BecomeVulnerable:
                state.EnemyVulnerableTurns = Mathf.Max(state.EnemyVulnerableTurns, effect.value);
                OnBossAction?.Invoke($"Vulnerable {effect.value}");
                break;

            case BossEffectType.ClearInvulnerability:
                state.EnemyInvulnerableTurns = 0;
                OnBossAction?.Invoke("Invulnerabilite retiree");
                break;

            case BossEffectType.StunSelf:
                state.EnemyStunnedTurns = Mathf.Max(state.EnemyStunnedTurns, effect.value);
                OnBossAction?.Invoke($"Etourdi {effect.value}");
                break;

            case BossEffectType.ClearStun:
                state.EnemyStunnedTurns = 0;
                OnBossAction?.Invoke("Etourdissement retire");
                break;

            case BossEffectType.Wait:
                OnBossAction?.Invoke("Attend");
                break;
        }
    }

    private void Say(string line, AudioClip clip)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            OnBossDialogue?.Invoke(line);
        }

        if (clip != null)
        {
            OnBossAudio?.Invoke(clip);
        }
    }

    private void SetBossVisual(Sprite sprite, float scale)
    {
        OnBossVisualChanged?.Invoke(sprite, Mathf.Max(0.01f, scale));

        if (sprite != null)
        {
            OnBossSpriteChanged?.Invoke(sprite);
        }
    }
}
