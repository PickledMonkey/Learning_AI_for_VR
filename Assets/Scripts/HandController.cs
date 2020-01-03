/*
 * This class is the primary controller for managing the Game rules 
*/
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;

public class HandController : MonoBehaviour {

	// Debug objects
	public Text debugText;
	public GameObject testObject;

	// Other entities for tracking the game's state
	public GameObject paddle;
	private SteamVR_TrackedController controller;
	public GameObject target;

	// Used to track the controller's velocity
	private Vector3 lastControllerPos = Vector3.zero;
	private Vector3 controllerVel = Vector3.zero;

	// Variables supporting the logic of the game itself for determining wins and losses
	private bool boardResetting = false;
	private int playerScore = 0;
	private int paddleScore = 0;
	private float gameLength = 120.0f;
	private float timeLeft = 0.0f; //s
	private bool gameOver = true;


	// Use this for initialization
	void Start() {

	}

	private void OnEnable()
	{
		controller = GetComponent<SteamVR_TrackedController>();

		controller.PadClicked += HandlePadClicked;
		controller.MenuButtonClicked += HandleMenuClicked;
	}

	private void OnDisable()
	{
		controller.PadClicked -= HandlePadClicked;
		controller.MenuButtonClicked -= HandleMenuClicked;
	}


	// Update is called once per frame
	void Update()
	{
		controllerVel = (transform.position - lastControllerPos) / Time.deltaTime;
		lastControllerPos = transform.position;

		timeLeft -= Time.deltaTime;
		if(timeLeft <= 0.0)
		{
			gameOver = true;
		}
		else
		{
			displayScore();
		}
	}

	// Manual Reset button handler for testing
	private void HandleMenuClicked(object sender, ClickedEventArgs e)
	{
		resetBoard();
		return;
	}

	// Button handler to start or reset the whole game
	private void HandlePadClicked(object sender, ClickedEventArgs e)
	{
		newGame();
	}

	// Starts a new game
	void newGame()
	{
		playerScore = 0;
		paddleScore = 0;
		timeLeft = gameLength;
		gameOver = false;
		resetBoard();
	}

	// Resets the game to a state which allows the Player and AI to score again
	void resetBoard()
	{
		boardResetting = false;
		transform.Find("Sphere").GetComponent<Renderer>().material.color = Color.white;
		target.GetComponent<Renderer>().material.color = Color.white;
		paddle.GetComponent<Renderer>().material.color = Color.white;
	}

	// Displays the current scores and time left in the game
	void displayScore()
	{
		debugText.text = "Time:" + timeLeft.ToString() + "\nPlayer: " + playerScore.ToString() + "\nAI: " + paddleScore.ToString();
	}

	void OnCollisionEnter(Collision col)
	{
		// Hand collided with Target. Award the player two points if he hasn't already collided with the Paddle recently.
		if (col.gameObject.tag == "Target")
		{
			int points = 0;
			if(transform.Find("Sphere").GetComponent<Renderer>().material.color == Color.white)
			{
				points = 2;
				transform.Find("Sphere").GetComponent<Renderer>().material.color = Color.green;
			}

			if (col.gameObject.GetComponent<Renderer>().material.color != Color.green)
			{
				if(!gameOver)
				{
					playerScore += points;
				}				
				col.gameObject.GetComponent<Renderer>().material.color = Color.green;
			}

			if (!boardResetting)
			{
				Invoke("resetBoard", 1); // Reset the board in 1 second
			}
		}

		// Hand collided with the Paddle. Aware the AI two points if the Player has not hit the Target recently, otherwise, award 1 point.
		if (col.gameObject.tag == "Paddle")
		{
			int points = 1;
			if (transform.Find("Sphere").GetComponent<Renderer>().material.color == Color.white)
			{
				points = 2;
				transform.Find("Sphere").GetComponent<Renderer>().material.color = Color.red;
			}

			if (col.gameObject.GetComponent<Renderer>().material.color != Color.red)
			{
				if (!gameOver)
				{
					paddleScore += points;
				}
				col.gameObject.GetComponent<Renderer>().material.color = Color.red;
			}


			if(!boardResetting)
			{
				Invoke("resetBoard", 1); // reset the board in 1 second
			}
		}
	}





}
