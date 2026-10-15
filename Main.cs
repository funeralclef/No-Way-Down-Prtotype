using Godot;

public partial class Main : Node
{
	[Export]
	public PackedScene MobScene { get; set; }

	[ExportGroup("Camera")]

	[Export(PropertyHint.Range, "1.0,12.0,0.5")]
	public float CameraFollowSpeed { get; set; } = 6.0f;

	[Export(PropertyHint.Range, "0.2,6.0,0.1")]
	public float CameraFallFollowSpeed { get; set; } = 1.4f;

	[Export(PropertyHint.Range, "1.0,3.0,0.1")]
	public float RushCameraSpeedMultiplier { get; set; } = 1.7f;

	[Export(PropertyHint.Range, "1.0,15.0,0.5")]
	public float ArenaCameraFollowSpeed { get; set; } = 9.0f;

	[Export(PropertyHint.Range, "0.2,4.0,0.05")]
	public float ArenaOverviewHoldTime { get; set; } = 1.35f;

	[Export(PropertyHint.Range, "0.2,4.0,0.05")]
	public float ArenaZoomInDuration { get; set; } = 1.4f;

	[Export(PropertyHint.Range, "24.0,52.0,0.5")]
	public float ArenaFollowViewSize { get; set; } = 34.0f;

	[Export(PropertyHint.Range, "0.0,10.0,0.5")]
	public float ArenaFollowForwardOffset { get; set; } = 2.5f;

	[Export(PropertyHint.Range, "0.0,6.0,0.25")]
	public float ArenaCameraEdgePadding { get; set; } = 1.5f;

	[Export(PropertyHint.Range, "0.0,8.0,0.5")]
	public float CameraForwardOffset { get; set; } = 8.0f;

	[Export(PropertyHint.Range, "0.5,5.0,0.25")]
	public float RunStartDistance { get; set; } = 1.5f;

	[Export(PropertyHint.Range, "0.0,12.0,0.5")]
	public float RunningCameraHeightIncrease { get; set; } = 6.0f;

	[Export(PropertyHint.Range, "0.0,12.0,0.5")]
	public float RunningCameraViewIncrease { get; set; } = 6.0f;

	[Export(PropertyHint.Range, "0.0,6.0,0.25")]
	public float RushCameraViewBonus { get; set; } = 2.0f;

	[Export(PropertyHint.Range, "0.5,8.0,0.25")]
	public float CameraRiseSpeed { get; set; } = 2.5f;

	private Marker3D _cameraPivot;
	private Camera3D _camera;
	private Player _player;
	private ProceduralWorld _world;
	private EncounterDirector _encounterDirector;
	private CameraArena _lockedArenaCamera;
	private CameraArena _cameraArenaFocus;
	private float _arenaCameraElapsed;
	private Control _locationBanner;
	private Label _locationLabel;
	private PanelContainer _momentumDisplay;
	private Label _momentumStateLabel;
	private Label _momentumHintLabel;
	private Label _momentumMultiplierLabel;
	private MomentumOverlay _momentumOverlay;
	private PanelContainer _nitroDisplay;
	private ProgressBar _nitroBar;
	private Label _nitroStateLabel;
	private MusicPlayer _musicPlayer;
	private ScoreLabel _scoreHud;
	private CustomizationProfile _customizationProfile;
	private MobileControls _mobileControls;
	private RunEndOverlay _runEndOverlay;
	private RunHudDirector _hudDirector;
	private RunPauseMenu _pauseMenu;
	private RunProgression _runProgression;
	private RunBuffHud _buffHud;
	private RunBuffEffects _buffEffects;
	private ArenaRewardOverlay _arenaRewardOverlay;
	private RunProgressHud _runProgressHud;
	private AbyssPressureFront _abyssPressure;
	private RunVisualAtmosphere _visualAtmosphere;
	private Tween _locationBannerTween;
	private float _cameraStartZ;
	private float _cameraStartHeight;
	private float _cameraStartViewSize;
	private Vector3 _playerStartPosition;
	private float _feedbackTime;
	private bool _runStarted;
	private int _currentLocation = -1;
	private int _presentedBiome = -1;
	private int _activeArenaSection = -1;
	private bool _arenaRewardSequenceActive;
	private bool _finaleDecisionPending;
	private bool _continuedBeyondFinal;
	private BossEncounter _bossEncounter;
	private readonly BossId _finalBossId = (BossId)GD.RandRange(0, 2);
	private int _overrunTier;
	private int _arenaRewardSection = -1;
	private PlayerJumpTrajectory _playerJumpTrajectory;
	private CombatFeedbackDirector _combatFeedback;
	private RunRouteStyle _chapterRouteStyle =
		RunRouteStyle.Neutral;

	private const int FinalArenaCount = 4;

	public override void _Ready()
	{
		_mobileControls =
			GetNode<MobileControls>("UserInterface/MobileControls");
		_runEndOverlay = GetNode<RunEndOverlay>(
			"UserInterface/Retry");
		_runEndOverlay.Initialize(_mobileControls);
		_runEndOverlay.RetryRequested += ReloadCurrentRun;
		_runEndOverlay.RefugeRequested += ReturnToRefuge;

		_cameraPivot = GetNode<Marker3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/Camera3D");
		_player = GetNode<Player>("Player");
		_player.CounterStomped += OnCounterStomped;
		_player.StompChainAdvanced += OnStompChainAdvanced;
		_player.SlipstreamDodge +=
			OnSlipstreamDodge;
		_combatFeedback =
			GetNode<CombatFeedbackDirector>(
				"RunSystems/CombatFeedbackDirector");
		_combatFeedback.Initialize(
			_player,
			_camera,
			this,
			GetNode<Control>("UserInterface"));
		_player.ComboImpact +=
			_combatFeedback.OnComboImpact;
		_player.ComboRescued +=
			_combatFeedback.OnComboRescued;
		_player.ComboEnded +=
			_combatFeedback.OnComboEnded;
		_locationBanner = GetNode<Control>("UserInterface/LocationBanner");
		_locationLabel = GetNode<Label>("UserInterface/LocationBanner/Margin/Label");
		_locationBanner.Hide();
		_momentumDisplay = GetNode<PanelContainer>("UserInterface/MomentumDisplay");
		_momentumStateLabel = GetNode<Label>("UserInterface/MomentumDisplay/Margin/Content/State");
		_momentumHintLabel = GetNode<Label>("UserInterface/MomentumDisplay/Margin/Content/Hint");
		_momentumMultiplierLabel = GetNode<Label>("UserInterface/MomentumDisplay/Margin/Content/Multiplier");
		_momentumOverlay = GetNode<MomentumOverlay>("UserInterface/MomentumOverlay");
		_nitroDisplay = GetNode<PanelContainer>(
			"UserInterface/NitroDisplay");
		_nitroBar = GetNode<ProgressBar>(
			"UserInterface/NitroDisplay/Margin/Content/Bar");
		_nitroStateLabel = GetNode<Label>(
			"UserInterface/NitroDisplay/Margin/Content/State");
		_playerJumpTrajectory =
			GetNode<PlayerJumpTrajectory>(
				"RunSystems/PlayerJumpTrajectory");
		_playerJumpTrajectory.Initialize(_player);
		_musicPlayer = GetNodeOrNull<MusicPlayer>("/root/MusicPlayer");
		_scoreHud = GetNode<ScoreLabel>("UserInterface/ScoreLabel");
		Control userInterface =
			GetNode<Control>("UserInterface");
		DisplaySettings displaySettings =
			BindRunSettings(
				userInterface);
		_pauseMenu =
			GetNode<RunPauseMenu>(
				"UserInterface/RunOverlays/RunPauseMenu");
		_pauseMenu.Initialize(
			displaySettings);
		_runProgression = new RunProgression();
		_buffEffects = new RunBuffEffects { Name = "RunBuffEffects" };
		_player.AddChild(_buffEffects);
		_buffHud = new RunBuffHud { Name = "RunBuffHud" };
		userInterface.GetNode("RunOverlays").AddChild(_buffHud);
		_buffHud.Initialize(_runProgression, _player, SynchronizeRunBuild);
		_buffEffects.Activated += _buffHud.AnnounceActivation;
		_pauseMenu.BindBuildMenu(_buffHud.OpenBuild);
        var skillHud = GD.Load<PackedScene>("res://ui/run_skill_hud.tscn").Instantiate<RunSkillHud>();
        skillHud.Runtime = _player.RunSkills; userInterface.GetNode("RunOverlays").AddChild(skillHud);
		_arenaRewardOverlay =
			GetNode<ArenaRewardOverlay>(
				"UserInterface/RunOverlays/ArenaRewardOverlay");
		_arenaRewardOverlay.Initialize(
			_runProgression);
		_arenaRewardOverlay.RewardChosen +=
			OnArenaRewardChosen;
		_arenaRewardOverlay.RouteChosen +=
			OnArenaRouteChosen;
		_arenaRewardOverlay.RunContinued +=
			OnFinalAscentContinued;
		_arenaRewardOverlay.RunFinished +=
			OnFinalAscentFinished;
		_hudDirector =
			GetNode<RunHudDirector>(
				"RunSystems/RunHudDirector");
		_hudDirector.Initialize(
			userInterface);
		_customizationProfile =
			GetNodeOrNull<CustomizationProfile>("/root/CustomizationProfile");
		_cameraStartZ = _cameraPivot.Position.Z;
		_cameraStartHeight = _camera.Position.Z;
		_cameraStartViewSize = _camera.Size;
		_playerStartPosition = _player.Position;
		_customizationProfile?.SyncBestScore(_scoreHud.BestScore);
		_customizationProfile?.ApplySelectedSkin(_player);

		_visualAtmosphere =
			GetNode<RunVisualAtmosphere>(
				"RunSystems/RunVisualAtmosphere");
		_visualAtmosphere.Initialize(
			_player);
		_world =
			GetNode<ProceduralWorld>(
				"RunSystems/ProceduralWorld");
		_world.Initialize(
			_player,
			_playerStartPosition.Z);
		_encounterDirector =
			GetNode<EncounterDirector>(
				"RunSystems/EncounterDirector");
		_encounterDirector.Initialize(
			MobScene,
			this,
			_player,
			_camera,
			_world,
			_scoreHud);
		_encounterDirector.MobSquashed +=
			OnMobSquashed;
		_encounterDirector.ArenaWaveStarted +=
			OnEncounterArenaWaveStarted;
		_encounterDirector.ArenaCompleted +=
			OnEncounterArenaCompleted;
		_player.ComboImpact +=
			_encounterDirector.OnComboImpact;
		_player.ComboEnded +=
			_encounterDirector.OnComboEnded;
		_world.SectionEntered += OnWorldSectionEntered;
		_world.ArenaStarted += OnArenaStarted;
		_world.TraversalEnemyRequested +=
			_encounterDirector.SpawnTraversalEnemy;
		_world.ConstructionEnemyRequested +=
			_encounterDirector.SpawnConstructionEnemy;
		_runProgressHud =
			GetNode<RunProgressHud>(
				"UserInterface/RunOverlays/RunProgressHud");
		_runProgressHud.Initialize(
			_player,
			_world);
		_hudDirector.AttachRunProgressHud(
			_runProgressHud);

		_abyssPressure =
			GetNode<AbyssPressureFront>(
				"RunSystems/AbyssPressureFront");
		_abyssPressure.Initialize(
			_player,
			_world,
			userInterface);

		_musicPlayer?.BeginRun();

		_world.UpdateForPlayer(_player);
	}

	public override void _ExitTree()
	{
		// Run progression is accumulated in memory so a stomp never performs
		// synchronous file I/O inside the physics frame. Scene transitions are
		// a safe point to persist the batch.
		_customizationProfile?.FlushPendingSave();
		if (IsInstanceValid(_scoreHud))
		{
			_scoreHud.FlushPendingBestScore();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_player))
		{
			return;
		}

		UpdateRunStarted();
		CameraArena activeArena = GetActiveCameraArena();
		UpdateArenaCameraPhase(activeArena, (float)delta);
		float targetX = 0.0f;
		float targetZ = _cameraStartZ;
		float followSpeed = CameraFollowSpeed;

		if (activeArena != null)
		{
			float arenaFollowBlend = GetArenaFollowBlend();
			float arenaViewSize = GetArenaTargetViewSize(activeArena);
			Vector2 viewMargins =
				GetArenaCameraViewMargins(arenaViewSize);
			Vector3 desiredFollowTarget =
				_player.GlobalPosition +
				Vector3.Forward * ArenaFollowForwardOffset;
			if (_bossEncounter?.IsActive == true)
				desiredFollowTarget = _player.GlobalPosition.Lerp(_bossEncounter.CameraFocus, .4f);
			Vector3 clampedFollowTarget =
				activeArena.ClampCameraTarget(
					desiredFollowTarget,
					viewMargins.X,
					viewMargins.Y);
			targetX = Mathf.Lerp(
				activeArena.GlobalPosition.X,
				clampedFollowTarget.X,
				arenaFollowBlend);
			targetZ = Mathf.Lerp(
				activeArena.CameraLockZ,
				clampedFollowTarget.Z,
				arenaFollowBlend);
			followSpeed = Mathf.Lerp(
				ArenaCameraFollowSpeed,
				CameraFollowSpeed,
				arenaFollowBlend);
		}
		else if (_runStarted)
		{
			// The focus stays ahead (negative Z), placing the player below the
			// screen centre and preserving more reaction space above.
			targetZ = Mathf.Min(_cameraStartZ, _player.Position.Z - CameraForwardOffset);
			bool followingUpward = targetZ < _cameraPivot.Position.Z;
			followSpeed = followingUpward ? CameraFollowSpeed : CameraFallFollowSpeed;

			if (followingUpward && _player.IsRushing)
			{
				followSpeed *= RushCameraSpeedMultiplier;
			}
		}

		float followWeight = 1.0f - Mathf.Exp(-followSpeed * (float)delta);
		Vector3 cameraPosition = _cameraPivot.Position;
		cameraPosition.X = Mathf.Lerp(cameraPosition.X, targetX, followWeight);
		cameraPosition.Z = Mathf.Lerp(cameraPosition.Z, targetZ, followWeight);
		_cameraPivot.Position = cameraPosition;

		UpdateRunningCamera(delta, activeArena);
		_encounterDirector?.UpdateArenaTacticalRead(
			(float)delta);
		UpdateMomentumFeedback(delta);
		_world.UpdateForPlayer(_player);
		_abyssPressure?.Advance(
			(float)delta,
			_runStarted,
			_currentLocation);
	}

	private void UpdateRunStarted()
	{
		Vector2 distanceFromStart = new(
			_player.Position.X - _playerStartPosition.X,
			_player.Position.Z - _playerStartPosition.Z);

		if (!_runStarted && distanceFromStart.LengthSquared() >= RunStartDistance * RunStartDistance)
		{
			_runStarted = true;
		}
	}

	private void UpdateRunningCamera(double delta, CameraArena activeArena)
	{
		if (!_runStarted)
		{
			return;
		}

		float rushAmount = _player.CurrentMomentumTier == Player.MomentumTier.Rush
			? Mathf.InverseLerp(_player.RushMomentumThreshold, _player.MaxMomentum, _player.Momentum)
			: 0.0f;

		// With an orthographic camera, moving it higher alone does not reveal
		// more of the level, so its view size grows at the same time.
		float riseWeight = 1.0f - Mathf.Exp(-CameraRiseSpeed * (float)delta);
		Vector3 raisedPosition = _camera.Position;
		float targetCameraHeight = _cameraStartHeight + RunningCameraHeightIncrease;
		if (activeArena == null)
		{
			targetCameraHeight += RushCameraViewBonus * rushAmount * 0.5f;
		}

		raisedPosition.Z = Mathf.Lerp(
			raisedPosition.Z,
			targetCameraHeight,
			riseWeight);
		_camera.Position = raisedPosition;
		float targetViewSize = activeArena != null
			? GetArenaTargetViewSize(activeArena)
			: _cameraStartViewSize +
				RunningCameraViewIncrease +
				RushCameraViewBonus * rushAmount;
		_camera.Size = Mathf.Lerp(
			_camera.Size,
			targetViewSize,
			riseWeight);
	}

	private void UpdateArenaCameraPhase(
		CameraArena activeArena,
		float deltaSeconds)
	{
		if (activeArena != _cameraArenaFocus)
		{
			_cameraArenaFocus = activeArena;
			_arenaCameraElapsed = 0.0f;
			return;
		}

		if (activeArena != null)
		{
			_arenaCameraElapsed += deltaSeconds;
		}
	}

	private float GetArenaFollowBlend()
	{
		float duration = Mathf.Max(0.05f, ArenaZoomInDuration);
		float progress = Mathf.Clamp(
			(_arenaCameraElapsed - ArenaOverviewHoldTime) / duration,
			0.0f,
			1.0f);
		return progress * progress * (3.0f - 2.0f * progress);
	}

	private float GetArenaTargetViewSize(CameraArena activeArena)
	{
		float overviewSize = activeArena.CameraViewSize > 0.0f
			? activeArena.CameraViewSize
			: _cameraStartViewSize + RunningCameraViewIncrease;
		float followSize = Mathf.Min(
			overviewSize,
			Mathf.Max(_cameraStartViewSize, ArenaFollowViewSize));
		if (_bossEncounter?.IsActive == true)
		{
			Vector3 separation = _bossEncounter.Core.GlobalPosition - _player.GlobalPosition;
			float aspect = GetViewport().GetVisibleRect().Size.Aspect();
			Vector3 focus = _bossEncounter.CameraFocus - _player.GlobalPosition;
            float framing = Mathf.Max(Mathf.Max(Mathf.Abs(separation.X), Mathf.Abs(focus.X) * 1.5f) / Mathf.Max(.5f, aspect), Mathf.Max(Mathf.Abs(separation.Z), Mathf.Abs(focus.Z) * 1.5f)) + 17;
			followSize = Mathf.Max(followSize, framing);
		}
		return Mathf.Lerp(
			overviewSize,
			followSize,
			GetArenaFollowBlend());
	}

	private Vector2 GetArenaCameraViewMargins(float viewSize)
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		float aspect = viewportSize.Y <= 0.0f
			? 16.0f / 9.0f
			: viewportSize.X / viewportSize.Y;
		// Extra-wide displays already reveal sky at the sides; limiting the
		// gameplay aspect keeps horizontal arena tracking responsive.
		aspect = Mathf.Min(aspect, 16.0f / 9.0f);
		float horizontalMargin =
			viewSize * 0.5f * aspect +
			ArenaCameraEdgePadding;
		float groundTilt =
			Mathf.Abs(_cameraPivot.GlobalBasis.Y.Dot(Vector3.Up));
		float longitudinalMargin =
			viewSize * 0.5f /
			Mathf.Max(0.35f, groundTilt) +
			ArenaCameraEdgePadding;
		return new Vector2(horizontalMargin, longitudinalMargin);
	}

	private CameraArena GetActiveCameraArena()
	{
		if (_activeArenaSection >= 0 && IsInstanceValid(_lockedArenaCamera))
		{
			return _lockedArenaCamera;
		}

		foreach (Node node in GetTree().GetNodesInGroup("camera_arena"))
		{
			if (node is CameraArena arena && arena.ContainsPlayer(_player))
			{
				return arena;
			}
		}

		return null;
	}

	private void UpdateMomentumFeedback(double delta)
	{
		float deltaSeconds = (float)delta;
		_feedbackTime += deltaSeconds;

		Player.MomentumTier tier = _player.CurrentMomentumTier;
		Color tierColor;
		float targetEdgeIntensity;
		float targetRushAmount;
		float targetSpeedBoostAmount =
			Mathf.Max(
				_player.SpeedBoostVisualIntensity,
				_player.NitroSpool);

		switch (tier)
		{
			case Player.MomentumTier.Medium:
				_momentumStateLabel.Text = "FLOW";
				tierColor = new Color(0.38f, 0.9f, 1.0f);
				targetEdgeIntensity = 0.42f;
				targetRushAmount = 0.0f;
				break;
			case Player.MomentumTier.Rush:
				_momentumStateLabel.Text = "RUSH";
				tierColor = new Color(1.0f, 0.58f, 0.12f);
				targetEdgeIntensity = 1.0f;
				targetRushAmount = 1.0f;
				break;
			default:
				_momentumStateLabel.Text = "";
				tierColor = new Color(0.74f, 0.78f, 0.84f);
				targetEdgeIntensity = 0.0f;
				targetRushAmount = 0.0f;
				break;
		}

		if (_player.AirStompChain >= 2)
		{
			float comboIntensity =
				(int)_player.CurrentComboFlowTier /
				(float)(int)Player.ComboFlowTier.Legendary;
			Color comboColor =
				_player.CurrentComboFlowTier switch
				{
					Player.ComboFlowTier.Rhythm =>
						new Color(0.28f, 0.92f, 1.0f),
					Player.ComboFlowTier.Dominant =>
						new Color(1.0f, 0.72f, 0.16f),
					Player.ComboFlowTier.Legendary =>
						new Color(1.0f, 0.28f, 0.82f),
					_ => tierColor
				};
			tierColor = tierColor.Lerp(
				comboColor,
				0.45f + comboIntensity * 0.35f);
			targetEdgeIntensity = Mathf.Max(
				targetEdgeIntensity,
				0.28f + comboIntensity * 0.72f);
			targetRushAmount = Mathf.Max(
				targetRushAmount,
				comboIntensity * 0.48f);
		}

		_momentumHintLabel.Text = "";
		_momentumMultiplierLabel.Text =
			$"×{_player.RewardMultiplier}";
		float momentumVisualWeight =
			tier switch
			{
				Player.MomentumTier.Rush => 1.0f,
				Player.MomentumTier.Medium => 0.78f,
				_ => _player.AirStompChain >= 2
					? 0.58f
					: 0.0f
			};
		_scoreHud?.SetMomentumVisual(
			tierColor,
			momentumVisualWeight);
		_nitroBar.Value = _player.NitroRatio * 100.0f;
		_nitroStateLabel.Text = _player.IsNitroOverdrive
			? "NITRO  REACTOR"
			: _player.IsRiftTeleportActive
				? "NITRO  RIFT CHAIN"
			: _player.IsGroundBurstActive
				? "NITRO  GROUND BURST"
			: _player.IsNitroBurning
				? $"NITRO  {Mathf.RoundToInt(
					_player.NitroThrottle * 100.0f)}%"
			: _player.NitroCharge <
				_player.NitroMinimumIgnitionCharge
				? "NITRO  EMPTY"
				: "NITRO";
		Color nitroColor = _player.IsNitroBurning
			? new Color(0.3f, 0.94f, 1.0f)
			: new Color(0.76f, 0.86f, 0.92f);
		_nitroDisplay.Modulate = nitroColor;
		_momentumDisplay.Modulate = Colors.White;
		_momentumStateLabel.Modulate =
			tierColor;
		_momentumMultiplierLabel.Modulate =
			tierColor;
		_momentumDisplay.PivotOffset = _momentumDisplay.Size * 0.5f;
		float pulse = tier == Player.MomentumTier.Rush
			? 1.0f + Mathf.Sin(_feedbackTime * 8.0f) * 0.035f
			: 1.0f;
		_momentumDisplay.Scale = Vector2.One * pulse;

		float feedbackWeight = 1.0f - Mathf.Exp(-6.0f * deltaSeconds);
		_momentumOverlay.Intensity = Mathf.Lerp(
			_momentumOverlay.Intensity,
			Mathf.Max(
				targetEdgeIntensity,
				targetSpeedBoostAmount * 0.92f),
			feedbackWeight);
		_momentumOverlay.RushAmount = Mathf.Lerp(
			_momentumOverlay.RushAmount,
			targetRushAmount,
			feedbackWeight);
		_momentumOverlay.SpeedBoostAmount = Mathf.Lerp(
			_momentumOverlay.SpeedBoostAmount,
			targetSpeedBoostAmount,
			feedbackWeight);

		float comboMusicIntensity =
			_player.AirStompChain >= 2
				? (int)_player.CurrentComboFlowTier /
					(float)(int)Player.ComboFlowTier.Legendary
				: 0.0f;
		comboMusicIntensity =
			Mathf.Max(
				comboMusicIntensity,
				(_abyssPressure?.Intensity ?? 0.0f) *
					0.58f);
		_musicPlayer?.SetGameplayIntensity(
			_player.MomentumRatio,
			comboMusicIntensity,
			targetSpeedBoostAmount);
	}

	private void OnCounterStomped(int scoreMultiplier)
	{
		_musicPlayer?.PunchCombo(3);
	}

	private void OnSlipstreamDodge(
		int scoreReward,
		float nitroReward,
		float speedImpulse,
		bool perfectDodge,
		Vector3 worldPosition,
		Vector3 passDirection)
	{
		_scoreHud?.AddScore(
			Mathf.Max(
				1,
				scoreReward));
		_musicPlayer?.PunchCombo(
			perfectDodge ? 4 : 2);
		if (perfectDodge)
		{
			_customizationProfile
				?.RecordPerfectDodge(
					_scoreHud?.BestScore ?? 0);
		}
	}

	private void OnStompChainAdvanced(
		int chainCount,
		int scoreMultiplier)
	{
		_musicPlayer?.PunchCombo(
			chainCount);
		_customizationProfile
			?.RecordComboPeak(
				chainCount,
				_scoreHud?.BestScore ?? 0);
	}

	private void OnWorldSectionEntered(string title, bool isArena, int sectionIndex)
	{
		_currentLocation = sectionIndex;
		_visualAtmosphere?.SetBiome(_world.CurrentBiomeId);
		_encounterDirector?.SetCurrentLocation(
			sectionIndex);
		if (_presentedBiome != _world.CurrentBiomeId)
		{
			_presentedBiome = _world.CurrentBiomeId;
			var biome = RunBiomeCatalog.Get(_presentedBiome);
			_locationLabel.Text = biome.Name + "  /  " + biome.Weather;
			ShowArenaBanner();
		}
	}

	private void OnArenaStarted(
		int sectionIndex,
		int waveCount)
	{
		if (_activeArenaSection >= 0 ||
			!_encounterDirector.BeginArena(
				sectionIndex,
				waveCount))
		{
			return;
		}

		_activeArenaSection = sectionIndex;
		_player.SetArenaCombatActive(true);
		if (_world.TryGetArenaCircle(
				sectionIndex,
				out Vector3 pressureArenaCenter,
				out float pressureArenaRadius))
		{
			_abyssPressure?.EnterArena(
				pressureArenaCenter,
				pressureArenaRadius);
		}
		_lockedArenaCamera = _world
			.GetNodeOrNull<Node>(
				$"Section{sectionIndex}")
			?.GetNodeOrNull<CameraArena>(
				"CameraArena");
		_musicPlayer?.EnterArena(
			Mathf.Max(1, waveCount));
		if (!_continuedBeyondFinal && _world.GetArenaOrdinal(sectionIndex) == FinalArenaCount &&
			_world.TryGetArenaCircle(sectionIndex, out Vector3 bossCenter, out float bossRadius))
		{
			_encounterDirector.PauseForReward();
			// The final guardian gets a clear stage; travel relays from earlier
			// sections must not drift through its attack warnings.
			foreach (Node node in GetTree().GetNodesInGroup("mob"))
				if (node is Mob mob) mob.QueueFree();
			_locationBannerTween?.Kill();
			_locationBanner.Hide();
			_bossEncounter = new BossEncounter { Name = "FinalGuardian" };
			AddChild(_bossEncounter);
			_bossEncounter.Defeated += OnBossDefeated;
			_bossEncounter.PhaseChanged += phase => _musicPlayer?.SetArenaWave(phase, 2);
			_world.TryGetArenaLayout(sectionIndex, out var bossLayout, out _);
			var tactics = _world.GetNode<Node3D>($"Section{sectionIndex}").GetNodeOrNull<ArenaTactics>("ArenaTactics");
			_bossEncounter.Begin(_finalBossId, _player, bossCenter, bossRadius,
				GetNode<Control>("UserInterface"), _encounterDirector.ActiveEncounterId, bossLayout, tactics, _world.GenerationSeed);
			_musicPlayer?.SetArenaWave(1, 2);
		}
	}

	private void OnBossDefeated()
	{
		_player.RewardDangerousSection(24);
		OnMobSquashed(20);
		CompleteArenaEncounter();
	}

	private void OnEncounterArenaWaveStarted(
		ArenaWavePlan plan)
	{
		_musicPlayer?.SetArenaWave(
			plan.DisplayWave,
			plan.TotalWaves);
		_locationLabel.Text =
			$"WAVE {plan.DisplayWave} / " +
			$"{plan.TotalWaves}";
		ShowArenaBanner();
	}

	private void OnEncounterArenaCompleted(
		int sectionIndex)
	{
		if (_bossEncounter?.IsActive == true) return;
		if (sectionIndex ==
			_activeArenaSection)
		{
			CompleteArenaEncounter();
		}
	}

	private void CompleteArenaEncounter()
	{
		if (_arenaRewardSequenceActive ||
			_activeArenaSection < 0 || _bossEncounter?.IsActive == true)
		{
			return;
		}

		// The arena remains physically sealed until both choices are made.
		// That turns the draft into a calm victory beat without opening an
		// exploitable gap where the player can leave while the world is paused.
		_arenaRewardSequenceActive = true;
		_arenaRewardSection =
			_activeArenaSection;
		_encounterDirector?.PauseForReward();
		_player.SetArenaCombatActive(false);
		_encounterDirector?.RetreatActiveMobs();
		_customizationProfile?.RecordArenaCleared(_scoreHud.BestScore);
		_locationLabel.Text = "ARENA CLEARED";
		ShowArenaBanner();
		_pauseMenu?.SetGameplayVisible(false);
		_buffHud?.SetAvailable(false);
		_runProgressHud?.Hide();
		_mobileControls?.SetSettingsMenuOpen(true);

		int arenaOrdinal =
			_world.GetArenaOrdinal(
				_arenaRewardSection);
		if (!_continuedBeyondFinal &&
			arenaOrdinal == FinalArenaCount)
		{
			_finaleDecisionPending = true;
			// The draft starts only if the player elects to continue. A player
			// who ends here has genuinely completed the run, rather than leaving
			// an unused reward behind on a results screen.
			_arenaRewardOverlay.ShowFinaleStage();
			return;
		}

		_runProgression.BeginArenaDraft();
		_arenaRewardOverlay.ShowRewardStage();
	}

	private void OnFinalAscentContinued()
	{
		if (!_arenaRewardSequenceActive ||
			!_finaleDecisionPending)
		{
			return;
		}

		_finaleDecisionPending = false;
		_continuedBeyondFinal = true;
		_runProgression.BeginArenaDraft();
	}

	private void OnFinalAscentFinished()
	{
		if (!_arenaRewardSequenceActive ||
			!_finaleDecisionPending)
		{
			return;
		}

		_finaleDecisionPending = false;
		_world.CompleteArena(
			_arenaRewardSection);
		_customizationProfile?.FlushPendingSave();
		_scoreHud?.FlushPendingBestScore();
		ReturnToRefuge();
	}

	private void OnArenaRewardChosen(int index)
	{
		if (!_arenaRewardSequenceActive ||
			!IsInstanceValid(_player) ||
			_runProgression.SelectedReward == null)
		{
			return;
		}

		RunUpgradeDefinition reward =
			_runProgression.SelectedReward;
		SynchronizeRunBuild();
		_buffHud?.Announce(reward);
		_buffEffects?.Refresh(reward.Id);
		_musicPlayer?.PunchCombo(
			Mathf.Max(
				3,
				_runProgression.ArenaNumber + 2));
	}

	private void SynchronizeRunBuild()
	{
		RunUpgradeDefinition newest = null;
		foreach (var definition in RunProgression.Catalog)
		{
			int rank = _runProgression.GetRank(definition.Id);
			if (_player.GetRunUpgradeRank(definition.Id) < rank) newest = definition;
			_player.ApplyRunUpgrade(definition.Id, rank);
		}
		_buffHud.Refresh();
		if (newest != null)
		{
			_buffHud.Announce(newest);
			_buffEffects.Refresh(newest.Id);
		}
	}

	private void OnArenaRouteChosen(int index)
	{
		if (!_arenaRewardSequenceActive)
		{
			return;
		}

		FinalizeArenaRewardSequence(
			_runProgression.SelectedRoute);
	}

	private void FinalizeArenaRewardSequence(
		RunRouteStyle routeStyle)
	{
		int completedSection =
			_arenaRewardSection;
		if (completedSection < 0)
		{
			return;
		}

		_chapterRouteStyle = routeStyle;
		_world.CompleteArena(completedSection);
		UpdateOverrunDifficulty();
		_world.ApplyChapterRoute(
			routeStyle,
			completedSection);
		_player.RewardDangerousSection(18.0f);
		_player.OnArenaCleared();
		_abyssPressure?.SetRouteStyle(
			routeStyle);
		_abyssPressure?.ExitArena(
			_encounterDirector
				.Settings
				.PostArenaBreatherTime);
		_visualAtmosphere?.SetRouteStyle(
			routeStyle);
		_musicPlayer?.ExitArena();
		_musicPlayer?.SetRouteStyle(
			routeStyle);
		_encounterDirector?.RetreatActiveMobs();
		_encounterDirector?.SetRunContext(
			_chapterRouteStyle,
			_overrunTier);
		_encounterDirector?.FinishArena(
			_encounterDirector
				.Settings
				.PostArenaBreatherTime);

		_activeArenaSection = -1;
		if (IsInstanceValid(_bossEncounter)) { _bossEncounter.QueueFree(); _bossEncounter = null; }
		_lockedArenaCamera = null;
		_arenaRewardSection = -1;
		_arenaRewardSequenceActive = false;
		_mobileControls?.SetSettingsMenuOpen(false);
		_pauseMenu?.SetGameplayVisible(true);
		_buffHud?.SetAvailable(true);
		_runProgressHud?.Show();
	}

	private void UpdateOverrunDifficulty()
	{
		_overrunTier = _continuedBeyondFinal
			? Mathf.Max(
				1,
				_world.CompletedArenaCount -
					FinalArenaCount + 1)
			: 0;
		_world.SetOverrunTier(_overrunTier);
		_abyssPressure?.SetOverrunTier(
			_overrunTier);
		_encounterDirector?.SetRunContext(
			_chapterRouteStyle,
			_overrunTier);
	}

	private static DisplaySettings BindRunSettings(
		Control userInterface)
	{
		return userInterface.GetNode<DisplaySettings>(
			"DesktopDisplay");
	}

	private void ShowArenaBanner()
	{
		_locationBannerTween?.Kill();
		Vector2 restPosition =
			_hudDirector?.LocationBannerRestPosition ??
			_locationBanner.Position;
		_locationBanner.Position =
			restPosition;
		_locationBanner.PivotOffset =
			_locationBanner.Size * 0.5f;
		_locationLabel.PivotOffset =
			_locationLabel.Size * 0.5f;
		_locationBanner.Modulate =
			new Color(
				1.0f,
				1.0f,
				1.0f,
				0.0f);
		_locationLabel.Modulate =
			Colors.White;
		_locationBanner.Scale =
			new Vector2(0.06f, 1.0f);
		_locationBanner.Show();

		_locationBannerTween = CreateTween();
		_locationBannerTween.TweenProperty(
				_locationBanner,
				"scale",
				Vector2.One,
				0.46f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		_locationBannerTween.Parallel()
			.TweenProperty(
				_locationBanner,
				"modulate:a",
				1.0f,
				0.26f)
			.SetDelay(0.08f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		_locationBannerTween.Chain()
			.TweenInterval(4.8f);
		_locationBannerTween.Chain()
			.TweenProperty(
				_locationLabel,
				"modulate:a",
				0.0f,
				0.18f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_locationBannerTween.Chain()
			.TweenProperty(
				_locationBanner,
				"scale",
				new Vector2(0.06f, 1.0f),
				0.56f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_locationBannerTween.Parallel()
			.TweenProperty(
				_locationBanner,
				"modulate:a",
				0.0f,
				0.38f)
			.SetDelay(0.12f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_locationBannerTween.Chain()
			.TweenCallback(
			Callable.From(
				() =>
				{
					_locationBanner.Hide();
					_locationBanner.Scale =
						Vector2.One;
					_locationBanner.Modulate =
						Colors.White;
					_locationLabel.Modulate =
						Colors.White;
				}));
	}

	private void OnMobSquashed(int rewardMultiplier)
	{
		int adjustedReward =
			_abyssPressure?.ApplyScoreRiskBonus(
				rewardMultiplier) ??
			rewardMultiplier;
		_scoreHud.OnMobSquashed(adjustedReward);
		if (IsInstanceValid(_player))
		{
			_player.RegisterRecoveryKill();
			_player.OnRunEnemyDefeated();
			_abyssPressure?.RewardEnemyDefeat(
				_player.AirStompChain);
		}
		_customizationProfile?.RecordKill(_scoreHud.BestScore);
	}

	private void OnPlayerHit()
	{
		_bossEncounter?.Stop();
		_buffHud?.SetAvailable(false);
		_encounterDirector?.StopAll();
		if (IsInstanceValid(_player))
		{
			_player.SetArenaCombatActive(false);
		}
		_mobileControls.DisableInput();
		_musicPlayer?.SetRunEnded();
		_abyssPressure?.Stop();
		_hudDirector?.SetRunEnded(true);
		_runEndOverlay.ShowResults(
			_scoreHud.Score,
			_scoreHud.BestScore,
			_scoreHud.IsNewRecord);
	}

	private void ReloadCurrentRun()
	{
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}

	private void ReturnToRefuge()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile(
			"res://refuge.tscn");
	}

	public override void _Input(InputEvent inputEvent)
	{
		GamepadInput.Observe(inputEvent);
		_runEndOverlay?.HandleInput(inputEvent);
	}

}
