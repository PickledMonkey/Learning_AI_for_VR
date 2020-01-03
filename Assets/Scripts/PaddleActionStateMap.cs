/*
 * ActionStateMap for recording and querying Actions for the AI
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Accord.Collections;
using System;

public class PaddleActionStateMap : ActionStateMap
{

	// Base Class for AI Actions.
	public abstract class PaddleAction : GameAction
	{
		public PaddleAction(string tag, double probability) : base(tag, probability)
		{

		}
	}

	// Action for executing behavior for the Attack Action of the AI
	public class PaddleAttackAction : PaddleAction
	{
		private const float maxEngagementDistance = 0.4f; // AI is discouraged from choosing this Action beyond this range
		private const float forcePower = 10.0f; // The force the AI uses to accelerate to Attack

		public PaddleAttackAction(string tag, double probability) : base(tag, probability)
		{
		}

		// Runs every frame. Executes the behavior of this Action. Targets and chases the Player
		public override bool execute(GameObject paddle, GameObject rHand, GameObject target)
		{
			Rigidbody rHandRB = rHand.GetComponent<Rigidbody>();
			Rigidbody paddleRB = paddle.GetComponent<Rigidbody>();

			Vector3 goalPosition = rHand.transform.position + (rHandRB.velocity * Time.deltaTime);
			if (paddle.transform.position == goalPosition)
			{
				return true;
			}

			Vector3 forceVector = (goalPosition - paddle.transform.position).normalized * forcePower * Time.deltaTime;

			paddleRB.AddForce(forceVector, ForceMode.VelocityChange);

			return false;
		}

		// Get the weighting for choosing this Action over other actions
		public override double getProbability(GameState state)
		{
			double probability = baseProbability;
			if(state.handDistToPaddle > maxEngagementDistance && state.paddleTtcToHand < 0.1)
			{
				probability *= 0.1;
			}
			else
			{
				probability *= 0.8;
			}

			return probability; //Math.Min(1.0, probability);
		}

		// Get a prediction for the next state if this Action is executed. Used in Minimax calculations
		public override GameObjectState predictNextState(GameObjectState currState)
		{
			Vector3 rHandPos = currState.rHandPos + (currState.rHandVel * currState.deltaTime);
			Vector3 rHandVel = currState.rHandVel;

			Vector3 goalPosition = currState.rHandPos + (currState.rHandVel * currState.deltaTime);
			Vector3 forceVector = (goalPosition - currState.paddlePos).normalized * forcePower;

			Vector3 paddleVel = currState.paddleVel + (forceVector * currState.deltaTime);
			Vector3 paddlePos = currState.paddlePos + (paddleVel * currState.deltaTime);

			return new GameObjectState(rHandPos, rHandVel, paddlePos, paddleVel, currState.targetPos, currState.targetVel, currState.deltaTime);
		}
	}

	// Class for executing the Defend behavior of the AI
	public class PaddleDefendAction : PaddleAction
	{
		private const float minDefendDist = 0.4f; // Prioritize defending if the Player is too far away
		private const float forcePower = 5.0f; // Acceleration the AI has when defending
		private const float maxDefendDistance = 0.5f; // Prevent the AI from getting too far away from the Target

		public PaddleDefendAction(string tag, double probability) : base(tag, probability)
		{
		}

		// Runs every frame. Tries to move to the midpoint between the Target and the Player
		public override bool execute(GameObject paddle, GameObject rHand, GameObject target)
		{
			Rigidbody rHandRB = rHand.GetComponent<Rigidbody>();
			Rigidbody paddleRB = paddle.GetComponent<Rigidbody>();

			Vector3 handPosition = rHand.transform.position + (rHandRB.velocity * Time.deltaTime);
			float defenseDistance = Mathf.Min((target.transform.position + handPosition).magnitude/2, maxDefendDistance);

			Vector3 goalPosition = (target.transform.position + ((handPosition - target.transform.position).normalized*defenseDistance));

			if (paddle.transform.position == goalPosition)
			{
				return true;
			}

			Vector3 forceVector = (goalPosition - paddle.transform.position).normalized * forcePower * Time.deltaTime;
			Vector3 brakeForceVector = -(paddleRB.velocity).normalized * forcePower * Time.deltaTime;
			paddleRB.AddForce(brakeForceVector, ForceMode.VelocityChange);

			paddleRB.AddForce(forceVector*2, ForceMode.VelocityChange);

			return false;
		}

		// Get weighting for choosing this Action over other Actions
		public override double getProbability(GameState state)
		{
			double probability = baseProbability;
			if (state.handDistToPaddle > minDefendDist)
			{
				probability *= 0.9;
			}
			else
			{
				probability *= 0.1;
			}

			return Math.Min(1.0, probability);
		}

		// Predicts the next State if this Action was chosen to execute. Used in minimax algorithm.
		public override GameObjectState predictNextState(GameObjectState currState)
		{
			Vector3 rHandPos = currState.rHandPos + (currState.rHandVel * currState.deltaTime);
			Vector3 rHandVel = currState.rHandVel;

			Vector3 handPosition = currState.rHandPos + (currState.rHandVel * currState.deltaTime);
			float defenseDistance = Mathf.Min((currState.targetPos + handPosition).magnitude / 2, maxDefendDistance);

			Vector3 goalPosition = (currState.targetPos + ((handPosition - currState.targetPos).normalized * defenseDistance));
			Vector3 brakeForceVector = -(currState.paddleVel).normalized * forcePower * currState.deltaTime;
			Vector3 forceVector = (goalPosition - currState.paddlePos).normalized * forcePower;

			Vector3 paddleVel = currState.paddleVel + (brakeForceVector) + (2*forceVector);
			Vector3 paddlePos = currState.paddlePos + (paddleVel * currState.deltaTime);

			return new GameObjectState(rHandPos, rHandVel, paddlePos, paddleVel, currState.targetPos, currState.targetVel, currState.deltaTime);
		}
	}

	// This class executes the Stop Action for the AI
	public class PaddleStopAction : PaddleAction
	{
		private const float minStopTtc = -0.1f; // Prioritize Stopping if the Player is moving away from the Paddle

		public PaddleStopAction(string tag, double probability) : base(tag, probability)
		{

		}

		// Stops the Paddle from moving immidiately
		public override bool execute(GameObject paddle, GameObject rHand, GameObject target)
		{
			Rigidbody paddleRB = paddle.GetComponent<Rigidbody>();

			paddleRB.velocity = Vector3.zero;			

			return true;
		}

		// Get weighting for choosing this Action over other Actions
		public override double getProbability(GameState state)
		{
			double probability = baseProbability;
			if (state.paddleTtcToHand < minStopTtc)
			{
				probability *= 1.0;
			}
			else
			{
				probability *= 0.1;
			}

			return probability;
		}

		// Predicts the next State if this Action was chosen to execute. Used in minimax algorithm.
		public override GameObjectState predictNextState(GameObjectState currState)
		{
			Vector3 rHandPos = currState.rHandPos + (currState.rHandVel * currState.deltaTime);
			Vector3 rHandVel = currState.rHandVel;

			Vector3 paddleVel = Vector3.zero;
			Vector3 paddlePos = currState.paddlePos;

			return new GameObjectState(rHandPos, rHandVel, paddlePos, paddleVel, currState.targetPos, currState.targetVel, currState.deltaTime);
		}
	}

	public class PaddleActionState : ActionState
	{
		public PaddleActionState(GameState state) : base(state)
		{

		}
	}

	// KDTree for storing all the ActionStates
	protected KDTree<PaddleActionState> actionStateMap = new KDTree<PaddleActionState>(4);

	//protected KDTree<float, PaddleActionState> actionStateMap = new KDTree<float, PaddleActionState>(4, null, null, metric: L2Norm);

	public PaddleActionStateMap()
	{

	}

	// Used for debugging
	public int getMapSize()
	{
		return actionStateMap.Count;
	}

	protected PaddleActionState addActionState(GameObjectState objState)
	{
		double[] stateVals = objState.getStateVals();
		GameState state = new GameState(stateVals);
		state.voxelize();

		PaddleActionState actionState = new PaddleActionState(state);
		actionState.addAction(new PaddleAttackAction("Attack", 0.5));
		actionState.addAction(new PaddleDefendAction("Defend", 0.5));
		actionState.addAction(new PaddleStopAction("Stop", 0.5));

		actionStateMap.Add(state.getVoxel(), actionState);

		return actionState;
	}

	// Get a sorted list of the best Actions for a given State. If the State is not in the map, then add it.
	public override List<Tuple<double, GameAction, ActionState>> getBestActions(GameObjectState currState)
	{
		List<NodeDistance<KDTreeNode<PaddleActionState>>> actionStates = actionStateMap.Nearest(currState.getStateVals(), GameState.voxel_radius);

		List<Tuple<double, GameAction, ActionState>> bestActions = new List<Tuple<double, GameAction, ActionState>>();
		if (actionStates.Count < 1)
		{
			PaddleActionState newState = addActionState(currState);
			bestActions.AddRange(newState.getWeightedActions());
		}
		else
		{
			foreach (var actionState in actionStates)
			{
				bestActions.AddRange(actionState.Node.Value.getWeightedActions());
			}
		}

		bestActions.Sort(
			delegate (Tuple<double, GameAction, ActionState> a1, Tuple<double, GameAction, ActionState> a2)
			{
				if (a1 == null && a2 == null) { return 0; }
				else if (a1 == null) { return 1; }
				else if (a2 == null) { return -1; }
				return a2.Item1.CompareTo(a1.Item1);
			}
		);


		return bestActions;
	}

	

}
