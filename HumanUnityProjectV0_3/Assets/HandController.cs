﻿/** 
 * @file HandController.cs
 * @author Blaze Sanders SpaceVR(TM) 
 * @date 05/18/2017
 * @link https://github.com/SpaceVR-O1/Human
 * @version 0.3
 *
 * @brief Track a HTC VIVE controller, get its position and velocity, and interact with GameObjects.
 *
 * @section DESCRIPTION
 * 
 * TO-DO???
 * This script gets the device number (1 to 4) for the controller at program load.
 * Device number 0 = VIVE HMD
 */

/**
* System.Timers contains threaded timers to help with UI timing.
* 
* System.Collections contains interfaces and classes that define 
* various collections of objects, such as lists, queues, bit arrays, 
* hash tables and dictionaries.
*
* System.Runtime.InteropServices contains members to support COM interop 
* and use of external DLL's, SDK's, and API's.
* 
* UnityEngine contains Unity's C# library to control the game engine.
* 
*/
using System;
using System.Timers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;



public class HandController : MonoBehaviour
{
  static public bool LOCAL_DEBUG_STATEMENTS_ON = false;
  static public string OPEN_HAND = "00000000";
  static public string FIST = "00011111";
  static public string PINKY_EXTENDED = "00000001";
  static public string RING_EXTENDED = "00000010";
  static public string MIDDLE_EXTENDED = "00000100";
  static public string INDEX_EXTENDED = "00001000";
  static public string THUMB_EXTENDED = "00010000";

  private bool movingToPosition = false;
  private bool rightArm = false;
  public bool autoUnlockingEnabled = true;
  public float OffsetX = 0.0f;
  public float OffsetY = -1.0f;
  public float OffsetZ = 0.5f;
  public float xMin = -1.0f;
  public float xMax = 2.5f;
  public float yMin = -1.5f;
  public float yMax = 1.8f;
  public float zMin = -1.15f;
  public float zMax = 1.8f;

  private bool handOpen = true;
  //If false hand is in closed fist
  private bool armsActive = false;
  //If false users arm movements don't cause the robot to move.
  static public int TEST_PASSED = 22;
  //Constant used inside the Kinova ARM_base.cpp file

  private Valve.VR.EVRButtonId triggerButton = Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger;
  //Map VIVE trigger button to ID
  private bool triggerButtonDown = false;
  //True when trigger button starts being pressed
  private bool triggerButtonUp = false;
  //True when trigger button starts being released
  private bool triggerButtonPressed = false;
  //True when trigger button is being held down

  private Valve.VR.EVRButtonId gripButton = Valve.VR.EVRButtonId.k_EButton_Grip;
  //Map VIVE side grip button to ID
  private bool gripButtonDown = false;
  //True when side grip buttons starts being pressed
  private bool gripButtonUp = false;
  //True when side grip buttons button starts being released
  private bool gripButtonPressed = false;
  //True when side grip buttons button is being held down

  private Valve.VR.EVRButtonId touchpad = Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad;
  private Valve.VR.EVRButtonId menuButton = Valve.VR.EVRButtonId.k_EButton_ApplicationMenu;

  private SteamVR_Controller.Device controller { get { return SteamVR_Controller.Input ((int)trackedHandObj.index); } }

  private SteamVR_TrackedObject trackedHandObj;

  private GameObject pickup;
  //Used by Unity3D collider and rigid body components to allow user interaction

  /**@brief Used for initialization of this class
   * 
   * @section DESCRIPTION
   * 
   * Start(): Is called before the first frame update only if 
   * the script instance is enabled. For objects added to the 
   * scene, the Start function will be called on all scripts before
   * Update, etc are called for any of them. 
   */
  void Start ()
  {

	trackedHandObj = GetComponent<SteamVR_TrackedObject> ();  //Left or right controller

	if (KinovaAPI.initSuccessful) {
	  // Send commands to arm at most every 5 ms
	  InvokeRepeating ("MoveArmToControllerPosition", 0.0f, 0.05f);
	  InvokeRepeating ("UnlockArm", 0.5f, 0.5f);
	}

	int leftIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Rightmost);
	int rightIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost);
	Debug.Log("right controller index: " + rightIndex);
	Debug.Log("left controller index: " + leftIndex);

	rightArm = (int)trackedHandObj.index == rightIndex;
  }
  //END START() FUNCTION
  void UnlockArm ()
  {
	if (autoUnlockingEnabled && !movingToPosition) {
	  Debug.Log("unlocking arm");
	  KinovaAPI.StopArm(rightArm);
	}
  }

  void MoveArmToControllerPosition ()
  {
	Vector3 controllerPosition = GetGlobalPosition ();
	Vector3 controllerRotation = GetLocalRotation ();

	if (controller.GetPress (triggerButton)) {
	  float pi = (float)Math.PI;
	  float xTarget = (controllerPosition.z + OffsetZ) * -1 + 1;
	  float yTarget = (controllerPosition.y + OffsetY) * -1;
	  float zTarget = (controllerPosition.x + OffsetX);

	  if (rightArm) {
		zTarget *= -1;
	  }

	  float eulerY = transform.rotation.eulerAngles.y;
	  float radY = eulerY * pi / 180f;
	  float kinovaRadY = radY < pi ? radY * -1f : radY - pi;
	  float eulerX = transform.rotation.eulerAngles.x;
	  if (kinovaRadY < 0 && eulerX > 90) { // turning left/up
		eulerX = 275 - (eulerX - 275);
	  }
	  if (eulerX < 90) { // turning right/down
		eulerX -= 180;
	  } 
	  if (kinovaRadY < 0 && eulerX < 0) { // turning left/down
		eulerX = -90 - (eulerX + 90);
	  }
	  float radX = eulerX * pi / 180f;
	  float kinovaRadX = radX < pi ? radX * -1f : radX - pi;
	  if (eulerX < 0) { // turning right/down
		kinovaRadX = radX;
	  }
	  if (rightArm) { // mirror X rotation
	    kinovaRadX = (kinovaRadX * -1) + 3;
	  }
	  float thetaYMin = -0.8f;
	  float thetaYMax = 1.4f;

	  if (yTarget > yMin && yTarget < yMax) {
		Debug.Log ("Arm Target Y within valid range!");
		Debug.Log ("Arm Target X: " + xTarget);
		if (xTarget > xMin && xTarget < xMax) {
		  Debug.Log ("Arm Target X within valid range!");
		  Debug.Log ("Arm Target Z: " + zTarget);
		  if (zTarget > zMin && zTarget < zMax) {
			Debug.Log ("Arm Target Z within valid range!");
			MoveArmNoThetaY (new KinovaAPI.Position (xTarget, yTarget, zTarget,
			     kinovaRadX, 1.4f, 0f));
		  }
		}
	  }
	}

	if (controller.GetPress (touchpad)) {
	  if (controller.GetAxis (touchpad).y > 0.5f) {
		Debug.Log ("Touchpad Up pressed");
		MoveArm (KinovaAPI.RaiseTheRoof);
	  } else if (controller.GetAxis (touchpad).y < -0.5f) {
		Debug.Log ("Touchpad Down pressed");
		KinovaAPI.StopArm(rightArm);
	  } else if (controller.GetAxis (touchpad).x > 0.5f) {
		Debug.Log ("Touchpad Right pressed");
		Debug.Log ("Fingers moved to: " +
			KinovaAPI.MoveFingers(rightArm, true, true, true, true, true));
//		MoveArm (KinovaAPI.StretchOut);
	  } else if (controller.GetAxis (touchpad).x < -0.5f) {
		Debug.Log ("Touchpad Left pressed");
		Debug.Log ("Fingers moved to: " +
		    KinovaAPI.MoveFingers(rightArm, false, false, false, false, false));
//		MoveArm (KinovaAPI.FlexBiceps);
	  }
	}
  } // END MoveArmToControllerPosition()

  /**@brief Update() is called once per game frame. 
   * 
   * section DESCRIPTION
   * 
   * Update(): Is the main workhorse function for frame updates.
   * While FixedUpdate() and and LateUpdate() add extra features.
   */
  void Update ()
  {

	Vector3 controllerPosition = GetGlobalPosition ();
	Vector3 controllerRotation = GetLocalRotation ();

	if (controller.GetPressDown (menuButton)) {
	  Debug.Log ("Menu pressed");
	  MoveArm (KinovaAPI.HomePosition);
	}

	if (controller.GetPressDown (triggerButton)) {
	  Debug.Log ("Trigger pressed");
//	  MoveArm (ArmTargetX, ArmTargetY, ArmTargetZ, ArmTargetThetaX, ArmTargetThetaY, ArmTargetThetaZ);
	}

	if (controller.GetPress (touchpad)) {
	  if (controller.GetAxis (touchpad).y > 0.5f) {
		Debug.Log ("Touchpad Up pressed");
		MoveArm (KinovaAPI.RaiseTheRoof);
	  } else if (controller.GetAxis (touchpad).y < -0.5f) {
		Debug.Log ("Touchpad Down pressed");
		KinovaAPI.StopArm(rightArm);
	  } else if (controller.GetAxis (touchpad).x > 0.5f) {
		Debug.Log ("Touchpad Right pressed down");
		PauseInterruptHeartbeat ();
		Debug.Log ("Fingers moved to: " +
			KinovaAPI.MoveFingers(rightArm, true, true, true, true, true));
//		MoveArm (KinovaAPI.StretchOut);
	  } else if (controller.GetAxis (touchpad).x < -0.5f) {
		Debug.Log ("Touchpad Left pressed down");
		PauseInterruptHeartbeat ();
		Debug.Log ("Fingers moved to: " +
		    KinovaAPI.MoveFingers(rightArm, false, false, false, false, false));
//		MoveArm (KinovaAPI.FlexBiceps);
	  }
	}

	if (controller.GetPressDown (gripButton)) {
	  Debug.Log ("Grip button pressed");
//	  MoveArm (Scooping);
	  KinovaAPI.MoveArmHome(rightArm);
	}

	if (Main.DEBUG_STATEMENTS_ON && LOCAL_DEBUG_STATEMENTS_ON) {
	  Debug.Log ("Controller #" + (int)trackedHandObj.index + " POSITION is:");
	  Debug.Log ("Global X = " + controllerPosition.x + " Local X =  " + this.transform.localPosition.x);
	  Debug.Log ("Global Y = " + controllerPosition.y + " Local Y =  " + this.transform.localPosition.y);
	  Debug.Log ("Global Z = " + controllerPosition.z + " Local Z =  " + this.transform.localPosition.z);

	  Debug.Log ("Controller #" + (int)trackedHandObj.index + " ROTATION is:");
	  Debug.Log ("Local thetaX =  " + this.transform.localPosition.x);
	  Debug.Log ("Local thetaY =  " + this.transform.localPosition.y);
	  Debug.Log ("Local thetaZ =  " + this.transform.localPosition.z);
	}



	//CAPTURE CONTRLLER BUTTON INTERACTION
	if (controller == null) {
	  if (Main.DEBUG_STATEMENTS_ON)
		Debug.Log ("Hand controller not found. Please turn on at least one HTC VIVE controller.");
	  return; //Stops null reference expections
	}

	if (controller.GetPressDown (gripButton) && pickup != null) {
	  if (armsActive == false) {			
		if (Main.DEBUG_STATEMENTS_ON)
		  Debug.Log ("Grip buttons " + ((int)trackedHandObj.index - 1) + " pressed, arm is unlocked.");
		pickup.transform.parent = this.transform;
		pickup.GetComponent<Rigidbody> ().isKinematic = true;  
		armsActive = true;
	  }//END ARMSACTIVE IF STATEMENT
	  else {
		if (Main.DEBUG_STATEMENTS_ON)
		  Debug.Log ("Grip buttons " + ((int)trackedHandObj.index - 1) + " pressed, arm is locked.");
		pickup.transform.parent = null;
		pickup.GetComponent<Rigidbody> ().isKinematic = false;
		armsActive = false;
	  }
	}//END GETPRESSDWN IF() STATEMENT

  }
  //END UPDATE() FUNCTION

  void PauseInterruptHeartbeat ()
  {
    float pauseSecs = 5f;
    Debug.Log ("pausing arm unlock/interrupt for " + pauseSecs
            + " seconds...");
    movingToPosition = true;
    Invoke ("ResumeInterruptHeartbeat", pauseSecs);
  }

  void ResumeInterruptHeartbeat ()
  {
    movingToPosition = false;
  }

  /**
   * meters for x, y, z
   * radians for thetaX, thetaY, thetaZ
   **/
  void MoveArm (float x, float y, float z, float thetaX, float thetaY, float thetaZ)
  {
	try {
	    Debug.Log("Kinova init: " + KinovaAPI.initSuccessful);
	    if (KinovaAPI.initSuccessful) {
		    PauseInterruptHeartbeat ();
			string which = rightArm ? "right" : "left";
			float actualX = rightArm ? x * -1 : x;
			Debug.Log ("Moving " + which + " arm to (" + actualX + ", " + y + ", " + z + ", " + thetaX + ", " + thetaY + ", " + thetaZ
					+ ")");
			KinovaAPI.MoveHand (rightArm, actualX, y, z, thetaX, thetaY, thetaZ);
		}
	} catch (EntryPointNotFoundException e) {
	  Debug.Log (e.Data);
	  Debug.Log (e.GetType ());
	  Debug.Log (e.GetBaseException ());
	}
  }
  /**
   * meters for x, y, z
   * radians for thetaX, thetaZ
   **/
  void MoveArmNoThetaY (float x, float y, float z, float thetaX, float thetaZ)
  {
	try {
	    if (KinovaAPI.initSuccessful) {
		    string which = rightArm ? "right" : "left";
			float actualX = rightArm ? x * -1 : x;
			Debug.Log ("Moving " + which + " arm to (" + actualX + ", " + y + ", " + z + ", " + thetaX + ", CURR_THETA_Y, " + thetaZ
					+ ")");
			KinovaAPI.MoveHandNoThetaY (rightArm, actualX, y, z, thetaX, thetaZ);
		}
	} catch (EntryPointNotFoundException e) {
	  Debug.Log (e.Data);
	  Debug.Log (e.GetType ());
	  Debug.Log (e.GetBaseException ());
	}
  }

  void MoveArm (KinovaAPI.Position position)
  {
	MoveArm (position.X, position.Y, position.Z, position.ThetaX, position.ThetaY, position.ThetaZ);
  }

  void MoveArmNoThetaY (KinovaAPI.Position position)
  {
	MoveArmNoThetaY (position.X, position.Y, position.Z, position.ThetaX, position.ThetaZ);
  }

  /**@brief OnTriggerEnter() is called on collider trigger events.
   * 
   * section DESCRIPTION
   * 
   * OnTriggerEnter(): TO-DO???
   */
  private void OnTriggerEnter (Collider collider)
  {
	if (Main.DEBUG_STATEMENTS_ON)
	  Debug.Log ("Colllider trigger ENTER");
	pickup = collider.gameObject;
  }

  /**@brief OnTriggerExit() is called on collider trigger events.
   * 
   * section DESCRIPTION
   * 
   * OnTriggerEnter(): TO-DO???
   */
  private void OnTriggerExit (Collider collider)
  {
	if (Main.DEBUG_STATEMENTS_ON)
	  Debug.Log ("Colllider trigger EXIT");
	pickup = null;
  }

  /**@brief GetGlobalPosition() returns X, Y, Z coordinate of hand controller  
 * 
 * section DESCRIPTION
 * 
 * GetPosition(): returns X, Y, Z float coordinates to
 * ??? decimal points of hand controller in the global reference frame.
 */
  public Vector3 GetGlobalPosition ()
  {

	Vector3 newPosition = new Vector3 ((float)this.transform.position.x, (float)this.transform.position.y, (float)this.transform.position.z);

	return newPosition;
  }

  /**@brief GetLocalPosition() returns X, Y, Z coordinate of hand controller  
  * 
  * section DESCRIPTION
  * 
  * GetPosition(): returns X, Y, Z float coordinates to 
  * ??? decimal points of hand controller in the Hand Mounted Display 
  * LOCAL reference frame.
  */
  public Vector3 GetLocalPosition ()
  {

	Vector3 newPosition = new Vector3 ((float)this.transform.localPosition.x, (float)this.transform.localPosition.y, (float)this.transform.localPosition.z);

	return newPosition;
  }

  /**@brief GetLocalRotation() returns thetaX, thetaY, thetaZ angles of hand controller  
  * 
  * section DESCRIPTION
  * 
  * GetPosition(): returns thetaX, thetaY, thetaZ float angles in
  * degrees to ??? decimal points of hand controller in the 
  * Hand Mounted Display LOCAL reference frame.
  */
  Vector3 GetLocalRotation ()
  {

	Vector3 newPosition = new Vector3 ((float)this.transform.localRotation.x, (float)this.transform.localRotation.y, (float)this.transform.localRotation.z);

	return newPosition;
  }

  /**@brief GetGlobalVelocity() returns X, Y, Y velocity vector of hand controller  
  * 
  * section DESCRIPTION
  * 
  * GetGlobalVelocity(): returns X, Y, Y velocity vector of hand 
  * controller in Unity3D units per second to ??? decimal points 
  * by calculating change in position of hand controller between 
  * two game engine frames renders / calls to Update().
  */
  Vector3 GetGlobalVelocity (Vector3 previousPosition)
  {

	float frameRate = (1 / Time.deltaTime);

	float newXvelocity = (previousPosition.x - this.transform.position.x) / frameRate;  
	float newYvelocity = (previousPosition.y - this.transform.position.y) / frameRate;
	float newZvelocity = (previousPosition.z - this.transform.position.z) / frameRate;

	Vector3 newVelocity = new Vector3 (newXvelocity, newYvelocity, newZvelocity);

	return newVelocity;
  }

  /**@brief GetGlobalAcceleration() returns X, Y, Y acceleration vector of hand controller  
  * 
  * section DESCRIPTION
  * 
  * GetGlobalAcceleration(): TO-DO???
  */
  Vector3 GetGlobalAcceleration (Vector3 previousVelocity)
  {

	Vector3 acceleration = new Vector3 (0.00f, -9.81f, 0.00f); //WRONG!!!

	return acceleration;
  }


}
//END HANDCONTROLLER CLASS
