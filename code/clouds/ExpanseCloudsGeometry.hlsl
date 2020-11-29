#ifndef EXPANSE_CLOUDS_GEOMETRY_INCLUDED
#define EXPANSE_CLOUDS_GEOMETRY_INCLUDED

#include "../common/shaders/ExpanseSkyCommon.hlsl"

/* Mirror from ExpanseCommon.cs. */
#define CloudGeometryType_Plane 0
#define CloudGeometryType_CurvedPlane 1
#define CloudGeometryType_Sphere 2
#define CloudGeometryType_BoxVolume 3
#define CloudGeometryType_CurvedBoxVolume 4

/* Mirror from ExpanseCommon.cs. */
#define CloudGeometryDimension_TwoD 2
#define CloudGeometryDimension_ThreeD 3

/* Interface for cloud geometry object. */
interface ICloudGeometry {
  /**
   * @return: whether planet space point p is in bounds of the cloud geometry.
   * */
  bool inBounds(float3 p);

  /**
   * @return: uv coordinate for planet space point p, with tiling factor tile
   * and uv offset offset. Assumes coordinate is within the bounds of the
   * geometry.
   * */
  float3 mapCoordinate(float3 p, int3 tile, float3 offset);

  /**
   * @return: starting and ending intersection distances, as, correspondingly,
   * (x, y) if this is a 3D volume. Or, just single intersection, if a 2D
   * volume.
   * */
  float2 intersect(float3 p, float3 d);

  /**
   * @return: same as intersect, unless this is a 2D cloud geometry,
   * in which case something is faked.
   * */
  float2 intersect3D(float3 p, float3 d);

  /**
   * @return: dimension of this cloud volume, used for lighting calculations.
   */
  int dimension();

  /**
   * @return: given a point p, and density attenuation distance and bias,
   * computes the density attenuation factor.
   */
  float densityAttenuation(float3 p, float distance, float bias);

  /**
   * @return: 0-1 value to use in height gradient computations.
   */
  float heightGradient(float3 p);
};








/**
 * @brief: 2D xz-aligned plane.
 */
class CloudPlane : ICloudGeometry {
  float2 xExtent, yExtent, zExtent;
  float height, apparentThickness;

  /* Takes into account apparent thickness to make volumetric shadow queries
   * simpler. */
  bool inBounds(float3 p) {
    return boundsCheck(p.x, xExtent)
      && boundsCheck(p.y, yExtent)
      && boundsCheck(p.z, zExtent);
  }

  /* Disregards y components of p and tile. */
  float3 mapCoordinate(float3 p, int3 tile, float3 offset) {
    float2 minimum = float2(xExtent.x, zExtent.x);
    float2 maximum = float2(xExtent.y, zExtent.y);
    float2 uv = (p.xz - minimum) / (maximum - minimum);
    uv = frac(uv * tile.xz + offset.xz);
    return float3(uv.x, 0, uv.y);
  }

  float2 intersect(float3 p, float3 d) {
    return intersectXZAlignedPlane(p, d, xExtent, zExtent, height);
  }

  /* Useful for self-shadowing hacks. */
  float2 intersect3D(float3 p, float3 d) {
    return intersectAxisAlignedBoxVolume(p, d, xExtent, yExtent, zExtent);
  }

  int dimension() {
    return CloudGeometryDimension_TwoD;
  }

  float densityAttenuation(float3 p, float distance, float bias) {
    float2 origin = float2(dot(xExtent, float2(1, 1))/2, dot(zExtent, float2(1, 1))/2);
    float distFromOrigin = length(origin - p.xz);
    return saturate(exp(-(distFromOrigin-bias)/distance));
  }

  float heightGradient(float3 p) {
    return 0; // We have no meaningful height gradient.
  }
};

CloudPlane CreateCloudPlane(float2 xExtent, float2 zExtent, float height,
  float apparentThickness) {
  CloudPlane c;
  c.xExtent = xExtent;
  c.zExtent = zExtent;
  c.height = height;
  c.apparentThickness = apparentThickness;
  c.yExtent = float2(height-apparentThickness/2, height+apparentThickness/2);
  return c;
}








/**
 * @brief: Subsection of a sphere around the planet.
 */
class CloudCurvedPlane : ICloudGeometry {
  float2 rExtent, xAngleExtent, zAngleExtent, xExtent, zExtent;
  float radius, apparentThickness;

  /* Takes into account apparent thickness to make volumetric shadow queries
   * simpler. */
  bool inBounds(float3 p) {
    float r = length(p);
    float sinXAngle = p.x/radius;
    float sinZAngle = p.z/radius;
    return boundsCheck(r, rExtent)
      && boundsCheck(sinXAngle, xAngleExtent)
      && boundsCheck(sinZAngle, zAngleExtent);
    return true;
  }

  /* Disregards y components of p and tile. */
  float3 mapCoordinate(float3 p, int3 tile, float3 offset) {
    float2 minimum = float2(xAngleExtent.x, zAngleExtent.x);
    float2 maximum = float2(xAngleExtent.y, zAngleExtent.y);
    float2 sinAngles = float2(p.x/radius, p.z/radius);
    float2 uv = (sinAngles - minimum) / (maximum - minimum);
    uv = frac(uv * tile.xz + offset.xz);
    return float3(uv.x, 0, uv.y);
  }

  float2 intersect(float3 p, float3 d) {
    float3 intersection = intersectSphere(p, d, radius);
    if (intersection.z > 0 && (intersection.x > 0 || intersection.y > 0)) {
      // TODO: may need to check second t if both are hits and first t fails.
      float t = minNonNegative(intersection.x, intersection.y);
      float3 o = p + t * d;
      float sinXAngle = o.x/radius;
      float sinZAngle = o.z/radius;
      if (boundsCheck(sinXAngle, xAngleExtent) && boundsCheck(sinZAngle, zAngleExtent)) {
        return float2(t, t);
      }
      if (intersection.x > 0 && intersection.y > 0) {
        /* If the second intersection was valid, bounds check it as well. */
        t = max(intersection.x, intersection.y);
        o = p + t * d;
        sinXAngle = o.x/radius;
        sinZAngle = o.z/radius;
        if (boundsCheck(sinXAngle, xAngleExtent) && boundsCheck(sinZAngle, zAngleExtent)) {
          return float2(t, t);
        }
      }
    }
    return float2(-1, -1);
  }

  /* Useful for self-shadowing hacks. */
  float2 intersect3D(float3 p, float3 d) {
    // We can use our existing sky intersection logic to intersect 2 spheres---
    // the lower and upper boundaries of the cloud layer---at once.
    SkyIntersectionData intersection = traceSkyVolumeValid(p, d, rExtent.x, rExtent.y);
    float3 o = p + intersection.endT * d;
    float sinXAngle = o.x/radius;
    float sinZAngle = o.z/radius;
    if (boundsCheck(sinXAngle, xAngleExtent) && boundsCheck(sinZAngle, zAngleExtent)) {
      return float2(intersection.endT, intersection.endT);
    }
    return float2(-1, -1);
  }

  int dimension() {
    return CloudGeometryDimension_TwoD;
  }

  float densityAttenuation(float3 p, float distance, float bias) {
    // HACK: just use x and z extents directly to determine the origin.
    float2 origin = float2(dot(xExtent, float2(1, 1))/2, dot(zExtent, float2(1, 1))/2);
    float distFromOrigin = length(origin - p.xz);
    return saturate(exp(-(distFromOrigin-bias)/distance));
  }

  float heightGradient(float3 p) {
    return 0; // We have no meaningful height gradient.
  }
};

CloudCurvedPlane CreateCloudCurvedPlane(float2 xExtent, float2 zExtent, float height,
  float apparentThickness) {
  CloudCurvedPlane c;
  c.radius = height;// + _planetRadius; TODO: why does this work??
  c.apparentThickness = apparentThickness;
  c.rExtent = float2(height-apparentThickness/2, height+apparentThickness/2);
  /* We assume that the bounds have been provided as an arc length, from which
   * we can readily extract the subtended angle. */
  c.xExtent = xExtent;
  c.zExtent = zExtent;
  c.xAngleExtent = sin(xExtent/c.radius);
  c.zAngleExtent = sin(zExtent/c.radius);
  return c;
}








/**
 * @brief: 3D axis-aligned box.
 */
class CloudBoxVolume : ICloudGeometry {
  float2 xExtent, yExtent, zExtent;

  bool inBounds(float3 p) {
    return boundsCheck(p.x, xExtent)
      && boundsCheck(p.y, yExtent)
      && boundsCheck(p.z, zExtent);
  }

  float3 mapCoordinate(float3 p, int3 tile, float3 offset) {
    // Here we use the x extent as the distance the y coordinate spans,
    // so that the aspect ratio of the noises doesn't get wonky and we
    // don't have to deal with sub-1 grid sizes for our noise.
    float3 minimum = float3(xExtent.x, yExtent.x, zExtent.x);
    float3 maximum = float3(xExtent.y, yExtent.y + (xExtent.y - xExtent.x), zExtent.y);
    float3 uv = (p - minimum) / (maximum - minimum);
    return frac(uv * tile + offset);
  }

  float2 intersect(float3 p, float3 d) {
    return intersectAxisAlignedBoxVolume(p, d, xExtent, yExtent, zExtent);
  }

  float2 intersect3D(float3 p, float3 d) {
    return intersect(p, d);
  }

  int dimension() {
    return CloudGeometryDimension_ThreeD;
  }

  float densityAttenuation(float3 p, float distance, float bias) {
    // Only attenuate based on x-z distance.
    float2 origin = float2(dot(xExtent, float2(1, 1))/2, dot(zExtent, float2(1, 1))/2);
    float distFromOrigin = length(origin - p.xz);
    return saturate(exp(-(distFromOrigin-bias)/distance));
  }

  float heightGradient(float3 p) {
    return (p.y - yExtent.x) / (yExtent.y - yExtent.x);
  }
};

CloudBoxVolume CreateCloudBoxVolume(float2 xExtent, float2 yExtent, float2 zExtent) {
  CloudBoxVolume c;
  c.xExtent = xExtent;
  c.yExtent = yExtent;
  c.zExtent = zExtent;
  return c;
}







/**
 * @brief: Subsection of a sphere shell around the planet.
 */
class CloudCurvedBoxVolume : ICloudGeometry {
  float2 rExtent, xAngleExtent, zAngleExtent;

  /* Takes into account apparent thickness to make volumetric shadow queries
   * simpler. */
  bool inBounds(float3 p) {
    float r = length(p);
    float sinXAngle = p.x/r;
    float sinZAngle = p.z/r;
    // return boundsCheck(r, rExtent)
    //   && boundsCheck(sinXAngle, xAngleExtent)
    //   && boundsCheck(sinZAngle, zAngleExtent);
    return true;
  }

  /* Disregards y components of p and tile. */
  float3 mapCoordinate(float3 p, int3 tile, float3 offset) {
    float3 minimum = float3(xAngleExtent.x, rExtent.x, zAngleExtent.x);
    float3 maximum = float3(xAngleExtent.y, rExtent.y, zAngleExtent.y);
    float r = length(p);
    float3 coordinate = float3(p.x/rExtent.y, r, p.z/rExtent.y);
    float3 uv = (coordinate - minimum) / (maximum - minimum);
    // return frac(uv * tile + offset);
    return float3(0, 0, 0);
  }

  float2 intersect(float3 p, float3 d) {
    // We can use our existing sky intersection logic to intersect 2 spheres---
    // the lower and upper boundaries of the cloud layer---at once.
    float3 lowerIntersection = intersectSphere(p, d, rExtent.x);
    float3 upperIntersection = intersectSphere(p, d, rExtent.y);
    bool lowerHit = (lowerIntersection.z > 0) && (lowerIntersection.x > 0 || lowerIntersection.y > 0);
    bool upperHit = (upperIntersection.z > 0) && (upperIntersection.x > 0 || upperIntersection.y > 0);

    // Return early if we hit nothing.
    if (!lowerHit && !upperHit) {
      return float2(-1, -1);
    }

    // And now for the complex logic about where we enter and exit this
    // volume...
    if (lowerHit && upperHit) {
      // We've hit both boundaries.
      // Check the bounds of our entry and exit points.
      // bool entryInBounds = boundsCheck(sinStart.x, xAngleExtent) && boundsCheck(sinStart.y, zAngleExtent);
    } else if (lowerHit) {
      // We've just hit the lower boundary.
    } else if (upperHit) {
      // We've just hit the upper boundary.
    }

    // float lowerT = minNonNegative(lowerIntersection.x, lowerIntersection.y);
    // float upperT = minNonNegative(upperIntersection.x, upperIntersection.y);
    //
    // float3 oStart = p + lowerT * d;
    // float3 oEnd = p + upperT * d;
    //
    // float2 sinStart = oStart.xz/rExtent.x; // TODO: could also use lower extent.
    // float2 sinEnd = oEnd.xz/rExtent.y;
    // if (
    //  && boundsCheck(sinEnd.x, xAngleExtent) && boundsCheck(sinEnd.y, zAngleExtent)) {
    //   return float2(lowerT, upperT);
    // }

    return float2(-1, -1);
  }

  float2 intersect3D(float3 p, float3 d) {
    return intersect(p, d);
  }

  int dimension() {
    return CloudGeometryDimension_ThreeD;
  }

  float densityAttenuation(float3 p, float distance, float bias) {
    // TODO: compute origin correctly
    float2 origin = float2(0, 0);
    float distFromOrigin = length(origin - p.xz);
    return saturate(exp(-(distFromOrigin-bias)/distance));
  }

  float heightGradient(float3 p) {
    return (length(p) - rExtent.x) / (rExtent.y - rExtent.x);
  }
};

CloudCurvedBoxVolume CreateCloudCurvedBoxVolume(float2 xExtent, float2 yExtent,
  float2 zExtent) {
  CloudCurvedBoxVolume c;
  c.rExtent = yExtent;
  /* We assume that the bounds have been provided as an arc length, from which
   * we can readily extract the subtended angle. We use the upper radius
   * to define the arc length. */
  c.xAngleExtent = sin(xExtent/c.rExtent.x);
  c.zAngleExtent = sin(zExtent/c.rExtent.y);
  return c;
}


#endif // EXPANSE_CLOUDS_GEOMETRY_INCLUDED
