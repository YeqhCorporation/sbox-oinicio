using Sandbox;
using Sandbox.Citizen;

public sealed class PlayerMovement : Component
{
	// Movement Properties
	[Property] public float GroundControl { get; set; } = 4.0f;

	[Property] public float AirControl { get; set; } = 0.1f;

	[Property] public float MaxForce { get; set; } = 50f;

	[Property] public float Speed { get; set; } = 160f;

	[Property] public float RunSpeed { get; set; } = 290f;

	[Property] public float CrouchSpeed { get; set; } = 90f;

	[Property] public float JumpForce { get; set; } = 400f;

	// Object References
	[Property] public GameObject Head { get; set; }
	[Property] public GameObject Body { get; set; }

	// Member Variables
	public Vector3 WishVelocity = Vector3.Zero;
	
	public bool IsCrouching = false;

	public bool IsSprinting = false;

	private CharacterController characterController;
	private CitizenAnimationHelper animationHelper;

	private TimeSince timeSinceLastFootstep = 0;
	private float footstepInterval = 0.5f;


	protected override void OnAwake()
	{
		characterController = Components.Get<CharacterController>();
		animationHelper = Components.Get<CitizenAnimationHelper>();
	}

	protected override void OnUpdate()
	{
		// Estados de correr e agachar do personagem piroca k k k
		UpdateCrouch();
		IsSprinting = Input.Down( "Run" );
		if(Input.Pressed( "Jump" )) Jump();
		RotateBody();
		UpdateAnimations();
		HandleFootstepSound(); // Chama a função para gerenciar os sons de passos
	}

	protected override void OnFixedUpdate()
	{
		BuildWishVelocity();
		Move();
	}

void HandleFootstepSound()
	{
		if (characterController.IsOnGround && characterController.Velocity.Length > 100f)
		{
			// Jogador está se movendo no chão
			if (timeSinceLastFootstep > footstepInterval)
			{
				PlayFootstepSound();
				timeSinceLastFootstep = 0; // Reinicia o temporizador dos passos

				// Ajusta o intervalo de acordo com a velocidade (passos mais rápidos ao correr)
				if (IsSprinting)
				{
					footstepInterval = 0.35f; // Intervalo menor ao correr
				}
				else if (IsCrouching)
				{
					footstepInterval = 0.6f; // Passos mais lentos ao agachar
				}
				else
				{
					footstepInterval = 0.5f; // Intervalo padrão ao caminhar
				}
			}
		}
	}

	// Função para tocar som de passo
	void PlayFootstepSound()
	{
		string surfaceType = GetSurfaceTypeUnderPlayer(); // Detecta o tipo de superfície
		string soundPath = GetFootstepSound(surfaceType); // Mapeia para o som correto
		Sound.Play(soundPath, characterController.WorldPosition); // Toca o som na posição do jogador
	}

	// Detecta o tipo de superfície sob o jogador
	string GetSurfaceTypeUnderPlayer()
	{
		var trace = Scene.Trace.Ray(characterController.WorldPosition, characterController.WorldPosition + Vector3.Down * 10f)
			.Run();

		if (trace.Hit && trace.Surface != null)
		{
			return trace.Surface.ResourceName; // Nome do material da superfície
		}

		return "default"; // Se não detectar, retorna um tipo padrão
	}

	// Mapeia os tipos de superfície para sons
	string GetFootstepSound(string surfaceType)
	{
		switch (surfaceType)
		{
			case "grass":
				return "sounds/footsteps/footstep-grass.sound"; // Som de passos na grama
			case "wood":
				return "sounds/footsteps/footstep-wood.sound"; // Som de passos na madeira
			case "concrete":
				return "sounds/footsteps/footstep-concrete.sound"; // Som de passos no concreto
			case "dirt":
				return "sounds/footsteps/footstep-dirt.sound"; // Som de passos na terra
			default:
				return "sounds/footsteps/footstep-grass.sound"; // Som de passos padrão
		}
	}
	void BuildWishVelocity()
	{
		WishVelocity = 0;

		var rot = Head.WorldRotation;
		if ( Input.Down( "Forward" ) ) WishVelocity += rot.Forward;
		if ( Input.Down( "Backward" ) ) WishVelocity += rot.Backward;
		if ( Input.Down( "Left" ) ) WishVelocity += rot.Left;
		if ( Input.Down( "Right" ) ) WishVelocity += rot.Right;

		WishVelocity = WishVelocity.WithZ( 0 );
		if(!WishVelocity.IsNearZeroLength) WishVelocity = WishVelocity.Normal;

		if ( IsCrouching ) WishVelocity *= CrouchSpeed;
		else if ( IsSprinting ) WishVelocity *= RunSpeed;
		else WishVelocity *= Speed;
	}

	void Move()
	{ 
		// Pegar Gravidade da parada ai po k k k
		var gravity = Scene.PhysicsWorld.Gravity;

		if(characterController.IsOnGround)
		{
			// Função de Friction/Acceleration
			characterController.Velocity = characterController.Velocity.WithZ( 0 );
			characterController.Accelerate( WishVelocity );
			characterController.ApplyFriction( GroundControl );
		}
		else
		{
			// Aplicar Gravidade/ air control
			characterController.Velocity += gravity * Time.Delta * 0.5f;
			characterController.Accelerate( WishVelocity.ClampLength(MaxForce) );
			characterController.ApplyFriction( AirControl );
		}

		// Mover o personaagem piroca
		characterController.Move();

		// 
		if(!characterController.IsOnGround)
		{
			characterController.Velocity += gravity * Time.Delta * 0.5f;
		}
		else
		{
			characterController.Velocity = characterController.Velocity.WithZ( 0 );
		}
	}

	void RotateBody()
	{
		if(Body is null) return;

		var targetAngle = new Angles(0, Head.WorldRotation.Yaw(), 0).ToRotation();
		float rotateDifference = Body.WorldRotation.Distance( targetAngle );

		if(rotateDifference > 50f || characterController.Velocity.Length > 10f)
		{
			Body.WorldRotation = Rotation.Lerp( Body.WorldRotation, targetAngle, Time.Delta * 2f);
		}
	}

	void Jump()
	{
		if(!characterController.IsOnGround) return;

		characterController.Punch(Vector3.Up * JumpForce);
		animationHelper?.TriggerJump();
	}

	void UpdateAnimations()
	{
		if(animationHelper is null) return;

		animationHelper.WithWishVelocity( WishVelocity );
		animationHelper.WithVelocity ( characterController.Velocity );
		animationHelper.AimAngle = Head.WorldRotation;
		animationHelper.IsGrounded = characterController.IsOnGround;
		animationHelper.WithLook(Head.WorldRotation.Forward, 1f, 0.75f, 0.5f);
		animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Run;
		animationHelper.DuckLevel = IsCrouching ? 1f : 0f;
	}

	void UpdateCrouch()
	{
		if(characterController is null) return;

		if(Input.Pressed("Crouch") && !IsCrouching)
		{
			IsCrouching = true;
			characterController.Height /= 2f;
		}

		if(Input.Released("Crouch") && IsCrouching)
		{
			IsCrouching = false;
			characterController.Height *= 2f;
		}
	
	}
}
