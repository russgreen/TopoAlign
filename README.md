# TopoAlign
![Revit Version](https://img.shields.io/badge/Revit%20Version-2019_--_2020-blue.svg) ![.NET](https://img.shields.io/badge/.NET-4.7-blue.svg) 
![Revit Version](https://img.shields.io/badge/Revit%20Version-2021_--_2024-blue.svg) ![.NET](https://img.shields.io/badge/.NET-4.8-blue.svg) 
![Revit Version](https://img.shields.io/badge/Revit%20Version-2025-blue.svg) ![.NET](https://img.shields.io/badge/.NET-8-blue.svg)

![GitHub last commit](https://img.shields.io/github/last-commit/russgreen/TopoAlign) 

Topo Align contains 5 commands to assist in working with topo surfaces or topo solids (2024+).

## Align to Element

Pick elements (walls, floors, roofs, pads) and align topo surfaces/solids to the top or bottom face with variable vertical offsets to avoid coincident surfaces if required.
You can also align topo surfaces/solids to individual edges and some limited support for aligning topo surfaces to families. There is still an early attempt at cleaning up (removing) existing topo points that are within the plan area of the element being used for the alignment. The results are mixed for very complex elements and some manual processing may still be required.

## Align to Topo

Pick a topo surface and a floor.  The points on the topo surface will be added to the floor so the floor ends up shaped and edited.  In 2024+ the floor is converted to a region on the topo solid.

## Points from Lines

Draw model lines and arcs below the topo surface and use this tool to project them onto the topo surface as additional topo points. This is useful when grading regions of the site and you don’t want any topo surface manipulation beyond a given point. If the lines are selected from a closed loop you are asked if you want to remove the existing topo points within the plan shape.

## Points along contours

Draw model lines as contour lines on levels/reference planes where you want to precisely control the proposed contour of the topo. Or draw the contours on a datum level below the topo and control the Z value of the points with the offset value. The command will prompt you to select the topo surface and then the contour lines. Topo points will be created along the model lines at the specified distances.

## Points at Intersection

Add topo points to the surface when a selected face (e.g. wall, floor, roof, mass) intersections the topo. The command will prompt you to select the topo surface and then the face. Topo points will be created when the triangulated surfaces intersect.

## Reset Region

This works in a plan view and will remove internal topo points from an edited topo surface and then copy the points inside the area picked from an existing topo surface. Effectively undo edits for part of a graded surface. This will not reset boundary points. It is recommended that if multiple topo surfaces are used in a project they are named to assist in picking them from dropdowns.

# General Usage Instructions
1. Launch the app from the ribbon button.
2. Select if you will use a single element or edge(s) to align your topo surface.
3. Set the divider edge distance. This value is the maximum spacing a straight edge will be divided by. Curved edges are automatically tessellated.
4. Set the vertical offset from the selected face.
5. Choose if the topo should be aligned to the top or bottom face of the element.
6. Click Align to Element. Click on the topo to be aligned then click on the element or edges.

To have a floor follow topography Align to Topo.  You’ll be prompted to pick a floor and then a topo and it will add the topo points to the floor to make the floor shape edited.

To use Points from Lines you must place model lines (lines and curves only) and then must be physically located below the topo surface.

The reset region asked for you to pick an existing topo surface in the existing project phase and a new topo surface (usually created as a graded region). You pick a rectangular region in a plan view and topo points within that region are deleted from the new topo surface and new points are copied across from the existing topo surface. This will not reset boundary points.

