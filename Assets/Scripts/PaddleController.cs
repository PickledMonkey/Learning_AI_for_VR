/**
 * This class is the primary controller for the AI.
 **/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class PaddleController : MonoBehaviour {

	public GameObject testObject;

	// Object Information for Tracking
	public GameObject target;
	public SteamVR_TrackedObject controller_L;
	public SteamVR_TrackedObject controller_R;
	public SteamVR_TrackedObject controller_hmd;

	// Maps for storing Action history
	private PlayerActionStateMap playerStateMap = new PlayerActionStateMap();
	private PaddleActionStateMap paddleStateMap = new PaddleActionStateMap();

	// Threading Helper Handles and Locks
	private Mutex actionLock = new Mutex();
	private Mutex currStateLock = new Mutex();

	private ManualResetEvent actionComplete = new ManualResetEvent(false);
	private ManualResetEvent closeThreads = new ManualResetEvent(false);
	private ManualResetEvent successEvent = new ManualResetEvent(false);
	private ManualResetEvent failEvent = new ManualResetEvent(false);

	private ManualResetEvent minimaxEndEvent = new ManualResetEvent(false);
	private ManualResetEvent minimaxTimeoutEvent = new ManualResetEvent(false);

	// Threads
	bool threadsStarted = false; // flag on whether or not the threads have started
	Thread actionSelectionThread = new Thread(selectAction);
	Thread playerObservationThread = new Thread(updatePlayerMap);
	Thread timeoutThread = new Thread(waitTimeout);

	// Used for tracking successes and failures
	private Color prevColor = Color.white;
	private Color prevTargetColor = Color.white;

	// Current Action selected. Will be updated every Update()
	private ActionStateMap.GameAction currAction = new PaddleActionStateMap.PaddleDefendAction("Defend", 0.5);

	// Current State of the game. Will be updated every Update()
	private ActionStateMap.GameObjectState currState = null;

	// Used to calculate the controller velocity
	private Vector3 lastControllerPos_R = Vector3.zero;
	private Vector3 controllerVel_R = Vector3.zero;

	// UI element for debugging
	public Text debugText;

	// Configurable maximum velocity of the Paddle to prevent it from accelerating forever and glitching out
	public float maximumVelocity;


	// Use this for initialization
	void Start () {

	}

	private void Awake()
	{

	}

	// Update is called once per frame
	void Update ()
	{
		// Start the Action Selection and Observation threads
		if (!threadsStarted)
		{
			actionSelectionThread.Start(this);
			playerObservationThread.Start(this);
			timeoutThread.Start(this);
			threadsStarted = true;
		}

		// Cap the velocity of the paddle
		Rigidbody rb = GetComponent<Rigidbody>();
		if (rb.velocity.magnitude > maximumVelocity)
		{
			rb.velocity = rb.velocity.normalized * maximumVelocity;
		}

		// Update the current state information
		controllerVel_R = (controller_R.transform.position - lastControllerPos_R) / Time.deltaTime;
		lastControllerPos_R = controller_R.transform.position;
		if (currStateLock.WaitOne(0))
		{
			currState = new ActionStateMap.GameObjectState(controller_R.transform.position, controllerVel_R, transform.position, GetComponent<Rigidbody>().velocity, target.transform.position, Vector3.zero, Time.deltaTime);
			currStateLock.ReleaseMutex();
		}

		actionLock.WaitOne(); // Lock to prevent the Action from changing while executing
		if (currAction != null)
		{
			if (currAction.execute(gameObject, controller_R.gameObject, target))
			{
				actionComplete.Set();
			}
		}
		actionLock.ReleaseMutex();

		handleSuccessEvent();
	}

	// Compares the color of the paddle and target for any changes and flags whether or not the Player or AI succeeded
	public void handleSuccessEvent()
	{
		Color currColor = gameObject.GetComponent<Renderer>().material.color;
		if (currColor == Color.red && currColor != prevColor)
		{
			successEvent.Set();
		}
		prevColor = currColor;

		currColor = target.gameObject.GetComponent<Renderer>().material.color;
		if (currColor == Color.green && currColor != prevTargetColor)
		{
			failEvent.Set();
		}
		prevTargetColor = currColor;
	}

	// Called on game exit
	private void OnDestroy()
	{
		closeThreads.Set();
	}

	// Timeout thread to prevent the Minimax Action Selector from running too long
	protected static void waitTimeout(object arg)
	{
		PaddleController __this = (PaddleController)arg;
		while (!(__this.closeThreads.WaitOne(0)))
		{
			if (!(__this.minimaxTimeoutEvent.WaitOne(0)))
			{
				if (__this.minimaxEndEvent.WaitOne(1000))
				{

				}
				else
				{
					__this.minimaxTimeoutEvent.Set();
				}
			}
			Thread.Sleep(100);
		}
	}

	// Update thread for the Player StateMap. Records movements of the Player
	protected static void updatePlayerMap(object arg)
	{
		Thread.Sleep(1000); // Sleep to prevent race conditions on game load

		PaddleController __this = (PaddleController)arg;
			
		ActionStateMap.GameObjectState localState = new ActionStateMap.GameObjectState(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 0.0f);
		ActionStateMap.GameObjectState prevState = new ActionStateMap.GameObjectState(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 0.0f);
		while (!(__this.closeThreads.WaitOne(0)))
		{
			if (__this.currStateLock.WaitOne(1000))
			{
				localState.copy(__this.currState);
				__this.currStateLock.ReleaseMutex();

				__this.playerStateMap.recordAction(prevState, localState);
				prevState.copy(localState);
				Thread.Sleep(100);
			}
		}
	}
	
	// Action Selection Thread, runs minimax on the current State to get the best Action. Also keeps a history and updates it based on the successes and fails of the AI
	protected static void selectAction(object arg)
	{
		Thread.Sleep(1000); // Sleep to prevent race conditions on game load

		PaddleController __this = (PaddleController)arg;

		ActionStateMap.GameObjectState localState = new ActionStateMap.GameObjectState(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 0.0f);

		int maxHistory = 1000;
		int historyIndex = 0;
		Tuple<ActionStateMap.GameAction, ActionStateMap.ActionState>[] previousActions = new Tuple<ActionStateMap.GameAction, ActionStateMap.ActionState>[maxHistory];

		while (!(__this.closeThreads.WaitOne(0)))
		{
			if (__this.currStateLock.WaitOne(1000))
			{
				localState.copy(__this.currState);
				__this.currStateLock.ReleaseMutex();

				ActionStateMap.GameObjectState currState = localState;
				Tuple<double, ActionStateMap.GameAction, ActionStateMap.ActionState> nextAction = __this.minimax(currState, 3);

				if (nextAction.Item2.getTag() != __this.currAction.getTag())
				{
					__this.setNextAction(nextAction.Item2);
				}

				// Store history and adjust weight on success and fail
				previousActions[historyIndex] = Tuple.Create(nextAction.Item2, nextAction.Item3);
				historyIndex++;
				if (historyIndex >= maxHistory)
				{
					bool success = __this.successEvent.WaitOne(0);
					bool fail = __this.failEvent.WaitOne(0);
					__this.successEvent.Reset();
					__this.failEvent.Reset();

					historyIndex = 0;
					for (int i = 0; i < maxHistory; i++)
					{
						if (success)
						{
							previousActions[i].Item2.updateActionProbabilities(previousActions[i].Item1, 1.0);
						}
						if(fail)
						{
							previousActions[i].Item2.updateActionProbabilities(previousActions[i].Item1, -1.0);
						}
					}
				}
			}
		}
	}		

	// Helper function for Minimax, traverses down a State's outecomes to get the best weight based on the predictions
	protected double minimax_helper(ActionStateMap.GameObjectState currState, int depth, bool maximizingPaddle)
	{
		if (maximizingPaddle)
		{
			List <Tuple<double, ActionStateMap.GameAction, ActionStateMap.ActionState>> bestActions = paddleStateMap.getBestActions(currState);
			if(depth == 0 || actionComplete.WaitOne(0) || minimaxTimeoutEvent.WaitOne(0))
			{
				return bestActions[0].Item1; // should be sorted from the getBestActions function
			}

			double bestAction = -1.0;
			foreach (var action in bestActions)
			{
				double actionResult = minimax_helper(action.Item2.predictNextState(currState), depth - 1, !maximizingPaddle);
				if(actionResult < 0.0)
				{
					actionResult = action.Item1;
				}

				if (actionResult > bestAction)
				{
					bestAction = actionResult;
				}
			}
			return bestAction;
		}
		else //maximizingPlayer
		{
			List<Tuple<double, ActionStateMap.GameAction, ActionStateMap.ActionState>> minActions = playerStateMap.getBestActions(currState);
			if(minActions.Count < 1)
			{
				return -1.0;
			}

			if (depth == 0 || actionComplete.WaitOne(0) || minimaxTimeoutEvent.WaitOne(0))
			{
				return minActions[0].Item1; // should be sorted from the getBestActions function
			}

			double minAction = double.MaxValue;
			foreach (var action in minActions)
			{
				double actionResult = minimax_helper(action.Item2.predictNextState(currState), depth - 1, !maximizingPaddle);
				if (actionResult < minAction)
				{
					minAction = actionResult;
				}
			}
			return minAction;
		}
	}

	// Main minimax function. Finds the highest weighted Action the AI has access to
	protected Tuple<double, ActionStateMap.GameAction, ActionStateMap.ActionState> minimax(ActionStateMap.GameObjectState currState, int depth)
	{
		minimaxEndEvent.Reset();
		minimaxTimeoutEvent.Reset();
		

		List<Tuple<double, ActionStateMap.GameAction, ActionStateMap.ActionState>> bestActions = paddleStateMap.getBestActions(currState);
		if (depth == 0 || actionComplete.WaitOne(0) || minimaxTimeoutEvent.WaitOne(0))
		{
			return bestActions[0]; // should be sorted from the getBestActions function
		}

		Tuple<double, ActionStateMap.GameAction, ActionStateMap.ActionState> bestAction = Tuple.Create(Double.MinValue, bestActions[0].Item2, bestActions[0].Item3);
		foreach (var action in bestActions)
		{
			double actionResult = minimax_helper(action.Item2.predictNextState(currState), depth - 1, false);

			if (actionResult < 0.0)
			{
				actionResult = action.Item1;
			}

			if (actionResult > bestAction.Item1)
			{
				bestAction = Tuple.Create(actionResult, action.Item2, action.Item3);
			}
		}

		minimaxEndEvent.Set();
		return bestAction;
	}


	// Sets the next Action for the AI to execute
	protected void setNextAction(ActionStateMap.GameAction nextAction)
	{
		actionLock.WaitOne();
		actionComplete.Reset();
		currAction = nextAction;
		actionLock.ReleaseMutex();
	}


}


/* Static State Machine Logic that was executed every Update()
 *			
			//ActionStateMap.GameState state = new ActionStateMap.GameState(currState.getStateVals());

			//if (state.paddleTtcToHand < -0.1)
			//{
			//	//debugText.text = state.paddleTtcToHand.ToString();
			//	//debugText.text = stopAction.getTag();
			//	//debugText.text = currState.valCalcToString();
			//	stopAction.execute(gameObject, controller_R.gameObject, target);
			//}
			//else
			//if (state.handDistToPaddle > 0.4)
			//{
			//	//debugText.text = defdAction.getTag();
			//	defdAction.execute(gameObject, controller_R.gameObject, target);
			//}
			//else
			//{
				//debugText.text = currAction.getTag() + " Cnt: " + actionCount.ToString() + " Timeouts: " + timeoutCount.ToString();
			//	if (currAction.execute(gameObject, controller_R.gameObject, target))
			//	{
			//		actionComplete.Set();
			//	}
			//} 
 */
