using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent (typeof (BoxCollider))]
[RequireComponent (typeof (MeshFilter))]

public class DrawIntersection : MonoBehaviour {
	/// <summary>
	/// The color of the line.
	/// </summary>
	public Color lineColor = Color.red;
	/// <summary>
	/// If false, hide all mesh in the scene except the plane
	/// </summary>
	public bool showMesh = true;
	/// <summary>
	/// The mesh objects.
	/// </summary>
	MeshInfo[] meshObjects;
	
	/// <summary>
	/// Mesh info class; contains MeshFilter, list of points, 
	/// and previous location and rotation
	/// </summary?
	public class MeshInfo {
		public Vector3 position;
		public Quaternion rotation;
		public MeshFilter meshObject;
		public List<Vector3> points;
		
		public MeshInfo(Vector3 _position, Quaternion _rotation, MeshFilter _meshObject) {
			position = _position;
			rotation = _rotation;
			meshObject = _meshObject;
		}
	}
	
	/// <summary>
	/// The last position of the plane
	/// </summary>
	Vector3 lastPos;
	
	/// <summary>
	/// The last rotation of the plane
	/// </summary>
	Quaternion lastRot;
	
	// Use this for initialization
	void Start () {
		meshObjects = new MeshInfo[0];
	}
	
	// Update is called once per frame
	void Update () {
		
		//at the start, or if more objects have been added to the scene
		//initialize the meshObjects array and set the rotations
		//and positions of each meshObject
		if(GameObject.FindObjectsOfType(typeof(MeshFilter)).Length != meshObjects.Length) {
			MeshFilter[] meshFilters = GameObject.FindObjectsOfType(typeof(MeshFilter)) 
				as MeshFilter[];
			meshObjects = new MeshInfo[meshFilters.Length];
			for(int i = 0; i <  meshFilters.Length; i++) {
				meshObjects[i] = new MeshInfo(
					meshFilters[i].transform.position,
					meshFilters[i].transform.rotation,
					meshFilters[i]);
			}
		}
		
		//false if the plane has not moved since last update
		bool planeMoved = false;
		
		//if the plane has moved or rotated since last update,
		//update it's location variables and set planeMoved to false
		if(transform.position != lastPos || transform.rotation != lastRot) {
			lastPos = transform.position;
			lastRot = transform.rotation;
			planeMoved = true;
		}
		
		//Main loop; checks if mesh objects are intersecting with the plane,
		//and if they have moved or rotated since last update
		for(int i = 0; i < meshObjects.Length; i++) {
			MeshFilter meshObject = meshObjects[i].meshObject;
			
			if(meshObject.GetComponent<Renderer>() == GetComponent<Renderer>())
				continue; //to not check the plane against itself
			meshObject.GetComponent<Renderer>().enabled = showMesh;
			
			//continue if object intersects with plane
			if(GetComponent<Renderer>().bounds.Intersects(meshObject.GetComponent<Renderer>().bounds)) {
				//update mesh info if the plane has been moved, or if the mesh's position
				//or rotation has been changed, or if the mesh has no intersection points.
				if(planeMoved || !meshObject.transform.position.Equals(meshObjects[i].position) ||
				!meshObject.transform.rotation.Equals(meshObjects[i].rotation) ||
				meshObjects[i].points == null) {
					meshObjects[i].position = meshObject.transform.position;
					meshObjects[i].rotation = meshObject.transform.rotation;
					
					FindIntersectionPoints(i);
				}
			} else meshObjects[i].points = null; //set intersection points to null if the object is 
												 //no longer intersecting with the plane
		}

		//draws the points found by FindIntersectionPoints() 
		for(int i = 0; i < meshObjects.Length; i++) {
			List<Vector3> points = meshObjects[i].points;
			if(points != null) {
				for(int j = 0; j < points.Count; j += 2) {
					if(points[j] == points[j + 1]) continue; //skip if points were discarded earlier 
					Debug.DrawLine(points[j], points[j+1], lineColor);
				}
			}
		}
		
		
	}
	
	/// <summary>
	/// Finds the intersection points.
	/// </summary>
	/// <param name='index'>
	/// Index: the meshObject index in the meshObjects array
	/// </param>
	void FindIntersectionPoints(int index) {
		Plane plane = new Plane(transform.TransformDirection(Vector3.up), transform.position);
		List<Vector3> points = new List<Vector3>();
		
		MeshFilter meshObject = meshObjects[index].meshObject;
		
		Mesh mesh = meshObject.mesh;
		if(mesh == null) return;
		
		Vector3[] vertices = mesh.vertices;
		//Vector3[] normals = mesh.normals;
		int[] triangles = mesh.triangles;
		
		//loop through triangles to find intersections or points on the plane
		for(int j = 0; j < (mesh.triangles.Length); j += 3) {
			
			//retrieve the vertices of the triangle
			Vector3 vertex1 = meshObject.transform.TransformPoint(vertices[triangles[j + 0]]);
			Vector3 vertex2 = meshObject.transform.TransformPoint(vertices[triangles[j + 1]]);
			Vector3 vertex3 = meshObject.transform.TransformPoint(vertices[triangles[j + 2]]);
			
			//find if each vertice lies directly on the plane
			bool onPlane1 = Mathf.Approximately(plane.GetDistanceToPoint(vertex1), 0f);
			bool onPlane2 = Mathf.Approximately(plane.GetDistanceToPoint(vertex2), 0f);
			bool onPlane3 = Mathf.Approximately(plane.GetDistanceToPoint(vertex3), 0f);

			//find if each vertex is in front of or behind the plane
			bool inFront1 = plane.GetSide(vertex1);
			bool inFront2 = plane.GetSide(vertex2);
			bool inFront3 = plane.GetSide(vertex3);
			
			Vector3? firstPoint = null;
			
			//Special handling for if a point is directly on the planes surface
			#region Handle Points on Plane
			if(onPlane1) {
				if(inFront2 == inFront3) continue;
				firstPoint = vertex1;
			}
			
			if(onPlane2) {
				if(inFront1 == inFront3) continue;
				if(firstPoint != null) {
					points.Add((Vector3)firstPoint);
					points.Add(vertex2);
					continue;
				} else firstPoint = vertex2;
			}
			
			if(onPlane3) {
				if(inFront1 == inFront2) continue;
				if(firstPoint != null) {
					points.Add((Vector3)firstPoint);
					points.Add(vertex3);
					continue;
				} else firstPoint = vertex3;
			}
			#endregion
			
			float rayDistance;
			Ray ray = new Ray(vertex1, vertex2 - vertex1);
			
			//For points 1 and 2, if neither are on the planes surface, check if they are on opposite sides,
			//if so, find the intersection point on the plane.
			//If the first point was found earlier, add it and the intersection point to the list and continue.
			#region Find Intersections
			if(!onPlane1 && !onPlane2 && inFront1 != inFront2 && plane.Raycast(ray, out rayDistance)) {
				if(firstPoint != null) {
					points.Add((Vector3)firstPoint);
					points.Add(ray.GetPoint(rayDistance));
					continue; 
				} else firstPoint = ray.GetPoint(rayDistance);
				
			}
			
			ray = new Ray(vertex2, vertex3 - vertex2);
			
			if(!onPlane2 && !onPlane3 && (inFront2 != inFront3) && plane.Raycast(ray, out rayDistance)) {
				if(firstPoint != null) {
					points.Add((Vector3)firstPoint);
					points.Add(ray.GetPoint(rayDistance));
					continue; 
				} else firstPoint = ray.GetPoint(rayDistance);
			}
			
			if(firstPoint == null) continue;
			
			ray = new Ray(vertex3, vertex1 - vertex3);

			if(!onPlane3 && !onPlane1 && (inFront3 != inFront1) && plane.Raycast(ray, out rayDistance)) {
				points.Add((Vector3)firstPoint);
				points.Add(ray.GetPoint(rayDistance));
			}
			#endregion
		}
		
		//makes sure that all points are on the plane's surface, and removes points outside of the planes bounding box
		#region Cleanup Points
		for(int i = 0; i < points.Count; i += 2) {
			Vector3 p1 = ProjectPointOnPlane(plane.normal, transform.position, points[i]);
			Vector3 p2 = ProjectPointOnPlane(plane.normal, transform.position, points[i + 1]);
			
			bool containsP1 = GetComponent<Collider>().bounds.Contains(p1);
			bool containsP2 = GetComponent<Collider>().bounds.Contains(p2);
			
			if(!containsP1 && !containsP2) {
				points[i] = points[i + 1] = Vector3.zero;
				continue;
			}
			
			if(!containsP1 && containsP2) {
				Ray ray = new Ray(p1, p2 - p1);
				float distance;
				
				GetComponent<Collider>().bounds.IntersectRay(ray, out distance);
				
				Vector3 newP = ray.GetPoint(distance);
				points[i] = newP;
			}
			
			if(containsP1 && !containsP2) {
				Ray ray = new Ray(p2, p1 - p2);
				float distance;
				
				GetComponent<Collider>().bounds.IntersectRay(ray, out distance);
				
				Vector3 newP = ray.GetPoint(distance);
				points[i + 1] = newP;
				Debug.Log(distance.ToString());
			}
					
		}
		#endregion
		
		meshObjects[index].points = points;
	}
	
	/// <summary>
	/// Projects the point on plane. Function from Math3d by Bit Barrel Media
	/// </summary>
	/// <returns>
	/// The point on plane.
	/// </returns>
	/// <param name='planeNormal'>
	/// Plane normal.
	/// </param>
	/// <param name='planePoint'>
	/// Plane point.
	/// </param>
	/// <param name='point'>
	/// Point.
	/// </param>
	public static Vector3 ProjectPointOnPlane(Vector3 planeNormal, Vector3 planePoint, Vector3 point){
 
		float distance;
		Vector3 translationVector;
 
		//First calculate the distance from the point to the plane:
		distance = Vector3.Dot(planeNormal, (point - planePoint));
 
		//Reverse the sign of the distance
		distance *= -1;
 
		//Get a translation vector
		translationVector = planeNormal.normalized * distance;
 
		//Translate the point to form a projection
		return point + translationVector;
	}	
}
