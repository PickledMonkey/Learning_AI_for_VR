/*
 * Base Class for the ActionStateMap. Allows common functionality between the Player and AI for storing and looking up Actions.
 */
 using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Accord.Collections;

public class ActionStateMap {

	// State describes a state of the game in specifics using Vectors
	public class GameObjectState
	{
		public Vector3 rHandPos;
		public Vector3 rHandVel;
		public Vector3 paddlePos;
		public Vector3 paddleVel;
		public Vector3 targetPos;
		public Vector3 targetVel;
		public float deltaTime;

		private const float velocityLimit = 0.05f;

		public GameObjectState(Vector3 rHandPos, Vector3 rHandVel, Vector3 paddlePos, Vector3 paddleVel, Vector3 targetPos, Vector3 targetVel, float deltaTime)
		{
			this.rHandPos = rHandPos;
			this.rHandVel = rHandVel;
			this.paddlePos = paddlePos;
			this.paddleVel = paddleVel;
			this.targetPos = targetPos;
			this.targetVel = targetVel;
			this.deltaTime = deltaTime;
		}

		public GameObjectState(GameObject paddle, GameObject target, GameObject controller_R, float deltaTime)
		{
			rHandPos = controller_R.gameObject.transform.position;
			rHandVel = controller_R.GetComponent<Rigidbody>().velocity;

			paddlePos = paddle.transform.position;
			paddleVel = paddle.GetComponent<Rigidbody>().velocity;

			targetPos = target.transform.position;
			targetVel = target.GetComponent<Rigidbody>().velocity;

			this.deltaTime = deltaTime;
		}

		public void copy(GameObjectState other)
		{
			if(other == null)
			{
				return;
			}

			rHandPos = other.rHandPos;
			rHandVel = other.rHandVel;
			paddlePos = other.paddlePos;
			paddleVel = other.paddleVel;
			targetPos = other.targetPos;
			targetVel = other.targetVel;
			deltaTime = other.deltaTime;
		}

		// Converts the GameObjectState into the less specific values for a GameState
		public double[] getStateVals()
		{
			Vector3 handPosToPaddle = rHandPos - paddlePos;
			Vector3 targetPosToHand = targetPos - rHandPos;

			double handDistToPaddle = handPosToPaddle.sqrMagnitude;
			double targetDistToHand = targetPosToHand.sqrMagnitude;

			double paddleTtcToHand;
			float paddleApproachAngle = 0.0f;
			if (paddleVel.magnitude < velocityLimit && rHandVel.magnitude < velocityLimit)
			{
				paddleTtcToHand = 0.0f;
			}
			else if (paddleVel.magnitude >= velocityLimit)
			{
				paddleApproachAngle = Vector3.Angle(handPosToPaddle, paddleVel);
				paddleTtcToHand = (handPosToPaddle.magnitude / paddleVel.magnitude) * Mathf.Cos((paddleApproachAngle * Mathf.PI) / 180);
			}
			else
			{
				Vector3 paddlePosToHand = paddlePos - rHandPos;
				paddleApproachAngle = Vector3.Angle(paddlePosToHand, rHandVel);
				paddleTtcToHand = (paddlePosToHand.magnitude / rHandVel.magnitude) * Mathf.Cos((paddleApproachAngle * Mathf.PI) / 180);
				paddleTtcToHand = Math.Max(0.0, paddleTtcToHand); //To prevent the user from moving slowly and keeping the paddle stuck
			}

			double handTtcToTarget;
			float handApproachAngle = 0.0f;
			if (rHandVel.magnitude < velocityLimit && targetVel.magnitude < velocityLimit)
			{
				handTtcToTarget = 0.0f;
			}
			else if (rHandVel.magnitude >= velocityLimit)
			{
				handApproachAngle = Vector3.Angle(targetPosToHand, rHandVel);
				handTtcToTarget = (targetPosToHand.magnitude / rHandVel.magnitude) * Mathf.Cos((handApproachAngle * Mathf.PI) / 180);
			}
			else
			{
				Vector3 handPosToTarget = rHandPos - targetPos;
				handApproachAngle = Vector3.Angle(handPosToTarget, targetVel);
				handTtcToTarget = (handPosToTarget.magnitude / targetVel.magnitude) * Mathf.Cos((handApproachAngle * Mathf.PI) / 180);
			}

			return new double[] { handDistToPaddle, targetDistToHand, paddleTtcToHand, handTtcToTarget };
		}

		// Return State values as a voxel
		public virtual double[] getVoxelStateVals()
		{
			double[] stateVals = getStateVals();
			return GameState.voxelize(stateVals);
		}
	}

	// Base Class for an Action for the AI or Player. Contains some default logic between all Actions
	public abstract class GameAction
	{
		protected String baseTag;
		protected double baseProbability;

		public GameAction(String tag, double probability)
		{
			baseTag = tag;
			baseProbability = probability;
		}

		public String getBaseTag()
		{
			return baseTag;
		}

		public double getBaseProbability()
		{
			return baseProbability;
		}

		public virtual String getTag()
		{
			return baseTag;
		}

		// Returns true if the action is complete. Returns false if it still has more work to do.
		public abstract bool execute(GameObject paddle, GameObject rHand, GameObject target);

		public abstract double getProbability(GameState state);

		// Used for updating the weight of the Action
		public virtual void updateProbability(double failWeight)
		{
			if(failWeight > 0.0)
			{
				baseProbability = Math.Min(1.0, baseProbability + (0.20*failWeight));
			}
			else
			{
				baseProbability = Math.Max(0.0, baseProbability + (0.20*failWeight));
			}
			
		}

		public abstract GameObjectState predictNextState(GameObjectState currState);
	}

	public class GameState
	{
		public static int voxel_precision = 1;
		public static float voxel_radius = 0.25f;
		public static int[] voxel_round_factor = new int[4] {4, 4, 4, 4};

		public double handDistToPaddle { get; set; }
		public double targetDistToHand { get; set; }
		public double paddleTtcToHand { get; set; }
		public double handTtcToTarget { get; set; }

		public GameState(float handDistToPaddle, float targetDistToHand, float paddleTtcToHand, float handTtcToTarget)
		{
			this.handDistToPaddle = handDistToPaddle;
			this.targetDistToHand = targetDistToHand;
			this.paddleTtcToHand = paddleTtcToHand;
			this.handTtcToTarget = handTtcToTarget;
		}

		public GameState(double[] vals)
		{
			handDistToPaddle = vals[0];
			targetDistToHand = vals[1];
			paddleTtcToHand = vals[2];
			handTtcToTarget = vals[3];
		}

		// Allows voxels to be rounded by increments of 0.5, 0.33, 0.25, 0.2, etc.
		private static double factorRound(double num, int precision, int factor)
		{
			if(factor == 0)
			{
				return num;
			}

			return Math.Round(num * factor, precision) / factor;
		}

		public static double[] voxelize(double[] stateVals)
		{
			for (int i = 0; i < stateVals.Length; i++)
			{
				stateVals[i] = factorRound(Math.Round(stateVals[i], voxel_precision), 0, voxel_round_factor[i]);
			}
			return stateVals;
		}

		public virtual void voxelize()
		{
			handDistToPaddle = factorRound(Math.Round(handDistToPaddle, voxel_precision), 0, voxel_round_factor[0]);
			targetDistToHand = factorRound(Math.Round(targetDistToHand, voxel_precision), 0, voxel_round_factor[1]);
			paddleTtcToHand = factorRound(Math.Round(paddleTtcToHand, voxel_precision), 0, voxel_round_factor[2]);
			handTtcToTarget = factorRound(Math.Round(handTtcToTarget, voxel_precision), 0, voxel_round_factor[3]);
		}

		public virtual double[] getVoxel()
		{
			return new double[]{
				factorRound(Math.Round(handDistToPaddle, voxel_precision), 0, voxel_round_factor[0]),
				factorRound(Math.Round(targetDistToHand, voxel_precision), 0, voxel_round_factor[1]),
				factorRound(Math.Round(paddleTtcToHand, voxel_precision), 0, voxel_round_factor[2]),
				factorRound(Math.Round(handTtcToTarget, voxel_precision), 0, voxel_round_factor[3])
			};
		}

		// Gets a value describing how close the AI is to winning
		public virtual double getProgressFactor()
		{
			return handDistToPaddle;
		}

		public override String ToString()
		{
			return "handDistToPaddle:\t" + Math.Round(handDistToPaddle, 3).ToString() + "\n" +
					"targetDistToHand:\t" + Math.Round(targetDistToHand, 3).ToString() + "\n" +
					"paddleTtcToHand:\t" + Math.Round(paddleTtcToHand, 3).ToString() + "\n" +
					"handTtcToTarget:\t" + Math.Round(handTtcToTarget, 3).ToString();
		}
	}

	// Base class for a combination of a State and set of Actions
	public abstract class ActionState
	{
		protected GameState state;
		protected List<GameAction> actions = new List<GameAction>();

		public ActionState(GameState state)
		{
			this.state = state;
		}

		public virtual List<GameAction> getActions()
		{
			return actions;
		}

		public virtual void addAction(GameAction newAction)
		{
			actions.Add(newAction);
		}

		// Updates all actions for the set. The chosenAction is affected the most by the failWeight, and the other Actions are affected by a reverse of the failWeight
		public virtual void updateActionProbabilities(GameAction chosenAction, double failWeight)
		{
			String chosenTag = chosenAction.getTag();
			for (int i = 0; i < actions.Count; i++)
			{
				if(actions[i].getTag().Equals(chosenTag))
				{
					actions[i].updateProbability(failWeight * 0.5);
				}
				else
				{
					actions[i].updateProbability(failWeight * (-1.0 / (double)actions.Count) * 0.5);
				}
			}
		}

		// Get a list of all the Actions for the this State and their weights
		public virtual List<Tuple<double, GameAction, ActionState>> getWeightedActions()
		{
			List<Tuple<double, GameAction, ActionState>> weightedActions = new List<Tuple<double, GameAction, ActionState>>();
			foreach (var action in actions)
			{
				double weight = state.getProgressFactor() * action.getProbability(state);
				weightedActions.Add(Tuple.Create(weight, action, this));
			}
			return weightedActions;
		}

	}

	// Use this for initialization
	public ActionStateMap()
	{

	}

	
	public virtual List<Tuple<double, GameAction, ActionState>> getBestActions(GameObjectState currState)
	{
		List <Tuple<double, GameAction, ActionState>> nextActions = new List<Tuple<double, GameAction, ActionState>>();
		return nextActions;
	}



}
