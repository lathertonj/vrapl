﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR;

public class PickUpObjects : MonoBehaviour {

    private SteamVR_TrackedObject trackedObj;
    private SteamVR_Controller.Device Controller
    {
        get { return SteamVR_Controller.Input((int)trackedObj.index); }
    }
    private void Awake()
    {
        trackedObj = GetComponent<SteamVR_TrackedObject>();
    }


    private GameObject collidingObject = null;
    private IControllerInputAcceptor collidingControllable = null;
    private GameObject collidingControllableObject = null;
    private GameObject objectInHand = null;
    private GameObject grippedObject = null;
    private GameObject objectBeingScaled = null;
    private IControllerInputAcceptor touchpadObject = null;
    private GameObject touchpadGameObject = null;
    private Transform doubleGrabObject = null;
    private WireController doubleGrabWire = null;
    private Vector3 scaleStartDifference;
    private Vector3 scaleInitialLocalTransformPosition;
    private float scaleStart;
    // for objects that interpret controller movements and move themself
    private IMoveMyself objectMovingItself = null;
    private Collider colliderMovingItself = null;
    private Collider collidedFrom = null;
    private Vector3 initialMovePosition;

    private void SetCollidingObject( Collider col )
    {
        if( collidingObject )
        {
            // already colliding
            return;
        }
        if( col.gameObject.GetComponent<PickUpObjects>() != null )
        {
            // don't collide with the other controller
            return;
        }

        Transform objectToTest = col.transform;
        while( objectToTest )
        {
            MovableController movableToTest = objectToTest.GetComponent<MovableController>();
            if( objectToTest.GetComponent<Rigidbody>() && ( movableToTest == null || movableToTest.amMovable ) )
            {
                collidingObject = objectToTest.gameObject;
                collidedFrom = col;
                StartOutliningObject();
                // this fires way too often and so is annoying. would need some sort of variable length debounce
                // Controller.TriggerHapticPulse( durationMicroSec: 500 );
                return;
            }
            objectToTest = objectToTest.parent;
        }

        // If we got here, we didn't find a colliding object.
        // this means we didn't collide with something that can be moved.
        // Check instead if we collided with something that can be controlled.
        objectToTest = col.transform;
        while( objectToTest )
        {
            IControllerInputAcceptor controllableToTest = (IControllerInputAcceptor) 
                objectToTest.GetComponent(typeof(IControllerInputAcceptor));
            if( controllableToTest != null )
            {
                collidingControllable = controllableToTest;
                collidingControllableObject = objectToTest.gameObject;
                return;
            }
            objectToTest = objectToTest.parent;
        }

    }

    public void OnTriggerEnter(Collider other)
    {
        SetCollidingObject( other );
    }

    public void OnTriggerStay(Collider other)
    {
        SetCollidingObject( other );
    }

    public void OnTriggerExit(Collider other)
    {
        StopOutliningObject();
        collidingObject = null;
        collidingControllable = null;
        collidingControllableObject = null;
        collidedFrom = null;
    }

    private void GrabObject()
    {
        Generator generator = SearchFor<Generator>( collidingObject.transform );
        if( generator != null )
        {
            // make a copy
            objectInHand = generator.GetCopy();

            // if it's a language object, let it know what prefab it came from
            LanguageObject lo = objectInHand.GetComponent<LanguageObject>();
            if( lo != null )
            {
                lo.prefabGeneratedFrom = PrefabStorage.GetName( generator.prefab );
            }
            
            // if we're in a function, parent it to the function
            if( TheRoom.InAFunction() )
            {
                objectInHand.transform.parent = TheRoom.GetCurrentFunction().GetBlockParent();
            }
        }
        else
        {
            objectInHand = collidingObject;
        }
        
        StopOutliningObject();
        collidingObject = null;

        // store myself on those who want it
        ControllerDataReporter controllerDataReporter = SearchFor<ControllerDataReporter>( objectInHand.transform );
        if( controllerDataReporter != null )
        {
            controllerDataReporter.myController = Controller;
            controllerDataReporter.myControllerPosition = transform;
        }

        JoinObjectToController( objectInHand );
    }

    private void JoinObjectToController( GameObject objectToJoin )
    {
        // check if objet wants to move itself instead
        IMoveMyself maybeSelfMoving = (IMoveMyself) objectToJoin.GetComponent( typeof(IMoveMyself) );
        if( maybeSelfMoving != null )
        {
            objectMovingItself = maybeSelfMoving;
            colliderMovingItself = collidedFrom;
            initialMovePosition = transform.position;
            objectMovingItself.StartMovement( colliderMovingItself, initialMovePosition );
            return;
        }

        if( !objectToJoin.GetComponent<MovableController>() )
        {
            objectToJoin.AddComponent<MovableController>();
        }
        if( UsePhysicalConnection( objectToJoin ) )
        {
            FixedJoint joint = AddFixedJoint();
            joint.connectedBody = objectToJoin.GetComponent<Rigidbody>();
        }
        else
        {
            objectToJoin.GetComponent<MovableController>().parentAfterMovement = objectToJoin.transform.parent;
            objectToJoin.transform.parent = transform;
        }
        if( objectToJoin )
        {
            objectToJoin.GetComponent<MovableController>().amBeingMoved = true;
            objectToJoin.GetComponent<MovableController>().amBeingMovedBy = transform;
        }
    }

    private void GrabAndDuplicateObject()
    {
        WorldObject collidingWorldObject = SearchFor< WorldObject >( collidingObject.transform );
        if( collidingWorldObject != null )
        {
            grippedObject = collidingWorldObject.MakeLanguageObjectDataReporter();
            
            StopOutliningObject();
            collidingObject = null;

            JoinObjectToController( grippedObject );

            // scale got changed by MakeLanguageObjectDataReporter so just make it be in my hand
            grippedObject.transform.localPosition = Vector3.zero;

            return;
        }

        LanguageObject collidingLanguageObject = SearchFor< LanguageObject >( collidingObject.transform );
        
        // OLD: only allow duplication if colliding language object has no chuck
        if( collidingLanguageObject != null )// && collidingLanguageObject.GetChuck() == null )
        {

            grippedObject = collidingLanguageObject.GetClone().gameObject;

            StopOutliningObject();
            collidingObject = null;

            JoinObjectToController( grippedObject );

            grippedObject.transform.localPosition = Vector3.zero;

            return;
        }

    }

    private bool UsePhysicalConnection( GameObject objectToJoin )
    {
        return objectToJoin.GetComponent<Rigidbody>() != null && !objectToJoin.GetComponent<Rigidbody>().isKinematic;
    }

    private FixedJoint AddFixedJoint()
    {
        FixedJoint fx = gameObject.AddComponent<FixedJoint>();
        fx.breakForce = 20000;
        fx.breakTorque = 20000;
        return fx;
    }

    private void ReleaseObject( GameObject objectToRelease )
    {
        if( objectMovingItself != null )
        {
            objectMovingItself.EndMovement( colliderMovingItself );
            objectMovingItself = null;
            colliderMovingItself = null;
            objectInHand = null;
            return;
        }
        if( objectToRelease )
        {
            objectToRelease.GetComponent<MovableController>().amBeingMoved = false;
            objectToRelease.GetComponent<MovableController>().amBeingMovedBy = null;

            if( UsePhysicalConnection( objectToRelease ) )
            {
                if( GetComponent<FixedJoint>() != null )
                {
                    GetComponent<FixedJoint>().connectedBody = null;
                    Destroy(GetComponent<FixedJoint>());

                    // give it the velocity of your hand
                    // TODO: is it the right thing to multiply by WorldSize.currentWorldSize?
                    // the controllers themselves or the arm movements will never be changing size...
                    objectToRelease.GetComponent<Rigidbody>().velocity = Controller.velocity;
                    objectToRelease.GetComponent<Rigidbody>().angularVelocity = Controller.angularVelocity;
                }
                else
                {
                    // fixed joint probably got popped off by an unstoppable force or immovable object
                    // the object is out there in the world
                    // let it be
                }
            } 
            else
            {
                objectToRelease.transform.parent = objectToRelease.GetComponent<MovableController>().parentAfterMovement;
                if( objectToRelease.GetComponent<MovableController>().additionalRelationshipChild )
                {
                    objectToRelease.GetComponent<MovableController>().additionalRelationshipChild.parent = 
                        objectToRelease.GetComponent<MovableController>().additionalRelationshipParent;
                }

                // give it zero velocity - stick where it is
                objectToRelease.GetComponent<Rigidbody>().velocity = Vector3.zero;
                objectToRelease.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
            
            }
        }
    }

    private void StartScalingObject()
    {
        if( objectBeingScaled != null )
        {
            return;
        }
        scaleStartDifference = transform.position - collidingObject.GetComponent<MovableController>().amBeingMovedBy.position;
        scaleStart = collidingObject.GetComponent<MovableController>().GetScale();
        scaleInitialLocalTransformPosition = collidingObject.transform.localPosition;
        objectBeingScaled = collidingObject;
    }

    private T SearchFor<T>( Transform objToSearch ) {
        while( objToSearch != null )
        {
            T t = objToSearch.GetComponent<T>();
            if( t != null )
            {
                return t;
            }

            objToSearch = objToSearch.parent;
        }
        return default(T);
    }

    private void SetScale()
    {
        Vector3 currentDifference = transform.position - objectBeingScaled.GetComponent<MovableController>().amBeingMovedBy.position;
        objectBeingScaled.GetComponent<MovableController>().SetScale( scaleStart * currentDifference.magnitude / scaleStartDifference.magnitude );
        // this isn't the correct adustment in local transform... the correct adjustment would also have a rotation...
        //objectBeingScaled.transform.localPosition = scaleInitialLocalTransformPosition + 0.5f * ( currentDifference - scaleStartDifference );
    }

    private void StopScalingObject()
    {
        objectBeingScaled = null;
    }

    private void StartOutliningObject()
    {
        // only show outline if renderers currently rendering i.e. language being shown
        if( collidingObject != null && RendererController.renderersCurrentlyRendering )
        {
            RendererController maybeRenderer = collidingObject.GetComponent<RendererController>();
            if( maybeRenderer != null )
            {
                maybeRenderer.SetOutlineEnabled( true );
            }
        }
    }

    private void StopOutliningObject()
    {
        if( collidingObject != null )
        {
            RendererController maybeRenderer = collidingObject.GetComponent<RendererController>();
            if( maybeRenderer != null )
            {
                maybeRenderer.SetOutlineEnabled( false );
            }
        }
    }

    private void StartDoubleGrabbingObject( GameObject o )
    {
        // start drawing a wire
        doubleGrabObject = o.transform;
        doubleGrabWire = Instantiate( PrefabStorage.GetPrefab( "wire" ) ).GetComponent<WireController>();
        // draw wire from o to controller
        doubleGrabWire.SetEndpoints( doubleGrabObject, transform );
    }

    private void StopDoubleGrabbingObject()
    {
        // if we're intersecting another object, try finalizing the wire between the two objects
        if( collidingObject != null && collidingObject.transform != doubleGrabObject )
        {
            // TODO: check if it's ok to draw the wire between these two things!
            doubleGrabWire.SetEndpoints( doubleGrabObject, collidingObject.transform );
        }
        // otherwise, delete the wire
        else
        {
            Destroy( doubleGrabWire.gameObject );
        }

        // reset my storage
        doubleGrabObject = null;
        doubleGrabWire = null;
    }

    private void DoOnTriggerUp()
    {
        // stop moving or scaling
        if( objectInHand )
        {
            ReleaseObject( objectInHand );
            objectInHand = null;
        }
        else if( objectBeingScaled )
        {
            StopScalingObject();
        }
    }

    public void Update()
    {
        // continue scaling or stop scaling the object
        if( objectBeingScaled )
        {
            // check if it's still being moved by the other controller. if it isn't, stop scaling it too
            if( objectBeingScaled.GetComponent<MovableController>() != null && 
                objectBeingScaled.GetComponent<MovableController>().amBeingMoved == false )
            {
                StopScalingObject();
            }
            else
            {
                // we can still scale the object
                SetScale();
            }
        }

        // move the object
        if( objectMovingItself != null )
        {
            objectMovingItself.Move( colliderMovingItself, initialMovePosition, transform.position );
        }


        // grab and move or scale an object
        if( Controller.GetHairTriggerDown() )
        {
            if( collidingObject )
            {
                if( collidingObject.GetComponent<MovableController>() != null &&
                    collidingObject.GetComponent<MovableController>().amBeingMoved == true )
                {
                    // only scale if it's being rendered right now
                    RendererController canRender = collidingObject.GetComponent<RendererController>();
                    if( canRender == null || canRender.beingRendered )
                    {
                        // already being moved by the other controller. start scaling it.
                        StartScalingObject();
                    }
                }
                else
                {
                    // only grab if it's being rendered right now
                    RendererController canRender = collidingObject.GetComponent<RendererController>();
                    if( canRender == null || canRender.beingRendered )
                    {
                        // grab it. nothing else is moving it right now.
                        GrabObject();
                    }
                }
            }
        }

        // stop moving or scaling
        if( Controller.GetHairTriggerUp() )
        {
            DoOnTriggerUp();
        }

        // send touchpad events
        if( Controller.GetPressDown( SteamVR_Controller.ButtonMask.Touchpad ) )
        {
            // TODO: if grabbed object exists, let's make a wire come out of it
            if( objectInHand != null )
            {
                GameObject objectToDoubleGrab = objectInHand;
                // simulate trigger being released
                DoOnTriggerUp();
                // start double-grab
                StartDoubleGrabbingObject( objectToDoubleGrab );
            }
            // otherwise, try to communicate with touchpad receiver
            else if( CollidingWithTouchpadReceiver() )
            {
                touchpadObject = GetTouchpadReceiver();
                touchpadGameObject = ( collidingControllableObject != null ) ? collidingControllableObject : collidingObject;
                touchpadObject.TouchpadDown();
            }
        }

        // stop sending touchpad events if the touchpad is no longer being clicked
        if( Controller.GetPressUp( SteamVR_Controller.ButtonMask.Touchpad ) )
        {
            // if doubleGrab object exists, let's clean up
            if( doubleGrabObject != null )
            {
                StopDoubleGrabbingObject();
            }

            // if touchpadObject is receiving data, lets send data end and clean up
            if( touchpadObject != null )
            {
                touchpadObject.TouchpadUp();
                touchpadObject = null;
                touchpadGameObject = null;
            }
        }

        // stop sending touchpad events if it's no longer being rendered
        if( touchpadObject != null)
        {
            RendererController canRender = touchpadGameObject.GetComponent<RendererController>();
            if( canRender != null && !canRender.beingRendered )
            {
                touchpadObject.TouchpadUp();
                touchpadObject = null;
                touchpadGameObject = null;
            }

        }

        // grip an object to copy it
        if( Controller.GetPressDown( SteamVR_Controller.ButtonMask.Grip ) )
        {
            if( collidingObject != null )
            {
                if( RendererController.renderersCurrentlyRendering )
                {
                    // only create a language object if language objects are currently rendering
                    GrabAndDuplicateObject();
                }
            }
        }


        // release the gripped object
        if( Controller.GetPressUp( SteamVR_Controller.ButtonMask.Grip ) )
        {
            if( grippedObject )
            {
                ReleaseObject( grippedObject );
                grippedObject = null;
            }
        }

        // send continuous info - touchpad 
        if( Controller.GetAxis() != Vector2.zero )
        { 
            if( touchpadObject != null )
            {
                touchpadObject.TouchpadAxis( Controller.GetAxis() );
            }
            else if( CollidingWithTouchpadReceiver() )
            {
                GetTouchpadReceiver().TouchpadAxis( Controller.GetAxis() );
            }
        }
        
        // send continuous info - transform
        if( touchpadObject != null )
        {
            touchpadObject.TouchpadTransform( transform );
        }

        
    }

    public bool UsingTouchpad()
    {
        return ( touchpadObject != null ) || CollidingWithTouchpadReceiver() || ( doubleGrabObject != null );
    }

    private bool CollidingWithTouchpadReceiver()
    {
        if( collidingObject || collidingControllable != null )
        {
            IControllerInputAcceptor inputAcceptor = GetTouchpadReceiver();
            if( inputAcceptor != null )
            {
                // only send info if it's being rendered right now
                RendererController canRender = null;
                if( collidingObject != null ) 
                {
                    canRender = collidingObject.GetComponent<RendererController>(); 
                }
                else if( collidingControllableObject != null )
                {
                    canRender = collidingControllableObject.GetComponent<RendererController>();
                } 

                if( canRender == null || canRender.beingRendered )
                {
                    return true;
                }
            }
        }
        return false;
    }

    private IControllerInputAcceptor GetTouchpadReceiver()
    {
        if( collidingControllable != null )
        {
            return collidingControllable;
        }
        return (IControllerInputAcceptor) collidingObject.GetComponent(typeof(IControllerInputAcceptor));
    }
}



public interface IMoveMyself
{
    void Move( Collider collider, Vector3 moveStart, Vector3 moveCurrent );
    void StartMovement( Collider collider, Vector3 start );
    void EndMovement( Collider collider );
}