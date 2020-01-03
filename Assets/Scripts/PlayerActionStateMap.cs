/*
 * ActionStateMap for recording and querying Actions the Player has made
 */
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Accord.Collections;

public class PlayerActionStateMap : ActionStateMap
{

	// Action describing the movement of the Player
	public class PlayerMoveAction : GameAction
	{
		private int probabilityUpdateCount = 0;

		public GameObjectState stateChange { set; get; } // Store the difference in GameObjectState when this Action happened

		public PlayerMoveAction(string tag, double probability) : base(tag, probability)
		{

		}

		// This function is not used because the probabilities are handled differently in this Action
		public override void updateProbability(double failWeight)
		{
			if (failWeight > 0.0)
			{
				baseProbability = baseProbability + 0.05;
			}
			else
			{
				baseProbability = Math.Max(0.0, baseProbability - 0.05);
			}
		}

		// Probability of this Action being chosen is the number of times its ActionState has been traversed divided by the number of times this Action was chosen
		public void updateProbabilityFromActionStateCount(int actionStateCount)
		{
			probabilityUpdateCount++;
			baseProbability = ((double)probabilityUpdateCount) / actionStateCount;
		}

		public override double getProbability(GameState state)
		{
			return baseProbability;
		}

		// Predict the next state if the Player moves in this manner. Used in minimax algorithm
		public override GameObjectState predictNextState(GameObjectState currState)
		{
			Vector3 rHandPos = currState.rHandPos + stateChange.rHandPos;
			Vector3 rHandVel = currState.rHandVel + stateChange.rHandVel;
			Vector3 paddlePos = currState.paddlePos + stateChange.paddlePos;
			Vector3 paddleVel = currState.paddleVel + stateChange.paddleVel;

			GameObjectState newState = new GameObjectState(rHandPos, rHandVel, paddlePos, paddleVel, currState.targetPos, currState.targetVel, currState.deltaTime);
			return newState;
		}

		// Action not meant to be executed. Just used to describe Player behavior
		public override bool execute(GameObject paddle, GameObject rHand, GameObject target)
		{
			return true;
		}
	}

	// Set of Actions paired with a particular state. Used to handle recording how the Player has moved
	public class PlayerActionState : ActionState
	{
		private KDTree<PlayerMoveAction> actionsAvailable = new KDTree<PlayerMoveAction>(4); // Store movement Actions in a KDTree so it's easier to query movements and update the probabilities
		private int numActionsMade = 0; // The number of times this State has been queried
		private int maxActionsMade = int.MaxValue; // The maximum number of times we care that this State happened. Allows a cap on the learning to prevent overlearning.

		public PlayerActionState(GameState state) : base(state)
		{

		}

		// Adds a new record of the Player's movement to the State if it doesn't exist. If it does, then it updates the Action's weight
		public void addAction(double[] stateVals, GameObjectState moveState)
		{
			List<NodeDistance<KDTreeNode<PlayerMoveAction>>> foundActions = actionsAvailable.Nearest(stateVals, GameState.voxel_radius);
			if (foundActions.Count < 1)
			{
				PlayerMoveAction newMove = new PlayerMoveAction("Move", 0.05);
				newMove.stateChange = moveState;
				actionsAvailable.Add(stateVals, newMove);
				addAction(newMove);
				if (numActionsMade < maxActionsMade)
				{
					numActionsMade++;
					newMove.updateProbabilityFromActionStateCount(numActionsMade);
				}
			}
			else
			{
				foreach (var action in foundActions)
				{
					if (numActionsMade < maxActionsMade)
					{
						numActionsMade++;
						action.Node.Value.updateProbabilityFromActionStateCount(numActionsMade);
					}
				}
			}
		}
	}

	// Map of the Player's Actions
	protected KDTree<PlayerActionState> actionStateMap = new KDTree<PlayerActionState>(4);

	public PlayerActionStateMap()
	{

	}

	// Used for debugging
	public int getMapSize()
	{
		return actionStateMap.Count;
	}

	// Attempts to retrieve the ActionState pairing for a particular GameState. If the ActionState is not in the Map, then it is added.
	private PlayerActionState getActionState(double[] stateVals)
	{
		List<NodeDistance<KDTreeNode<PlayerActionState>>> actionStates = actionStateMap.Nearest(stateVals, GameState.voxel_radius*0.1);
		if(actionStates.Count < 1)
		{
			PlayerActionState newState = new PlayerActionState(new GameState(stateVals));
			actionStateMap.Add(stateVals, newState);
			return newState;
		}

		return actionStates[0].Node.Value;
	}

	// Records observed movement of the Player to the Map
	public void recordAction(GameObjectState prevState, GameObjectState currState)
	{
		if(prevState == null || currState == null)
		{
			return;
		}		

		double[] prevVoxels = prevState.getVoxelStateVals();
		double[] currVoxels = prevState.getVoxelStateVals();

		PlayerActionState prevActionState = getActionState(prevVoxels);
		PlayerActionState currActionState = getActionState(currVoxels);

		Vector3 rHandPos = currState.rHandPos - prevState.rHandPos;
		Vector3 rHandVel = currState.rHandVel - prevState.rHandVel;
		Vector3 paddlePos = currState.paddlePos - prevState.paddlePos;
		Vector3 paddleVel = currState.paddleVel - prevState.paddleVel;
		GameObjectState stateChange = new GameObjectState(rHandPos, rHandVel, paddlePos, paddleVel, currState.targetPos, currState.targetVel, currState.deltaTime);

		prevActionState.addAction(currVoxels, stateChange);
	}

	// Gets a list of the best Actions for the Player to do. Used to predict the Player's movement in the minimax algorithm.
	public override List<Tuple<double, GameAction, ActionState>> getBestActions(GameObjectState currState)
	{
		List<NodeDistance<KDTreeNode<PlayerActionState>>> actionStates = actionStateMap.Nearest(currState.getStateVals(), GameState.voxel_radius);

		List<Tuple<double, GameAction, ActionState>> bestActions = new List<Tuple<double, GameAction, ActionState>>();

		if (actionStates.Count < 1)
		{
			return bestActions;
		}

		
		foreach (var actionState in actionStates)
		{
			bestActions.AddRange(actionState.Node.Value.getWeightedActions());
		}

		bestActions.Sort(
			delegate (Tuple<double, GameAction, ActionState> a1, Tuple<double, GameAction, ActionState> a2)
			{
				if (a1 == null && a2 == null) { return 0; }
				else if (a1 == null) { return 1; }
				else if (a2 == null) { return -1; }
				return a1.Item1.CompareTo(a2.Item1); // Reversed from Paddle so that smallest filter to the top
			}
		);
		return bestActions;
	}
}
