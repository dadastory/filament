//==============================================================================================================================
// An optimized AMD FSR's EASU implementation for Mobiles
// Based on https://github.com/GPUOpen-Effects/FidelityFX-FSR/blob/master/ffx-fsr/ffx_fsr1.h
// Details can be found: https://atyuwen.github.io/posts/optimizing-fsr/
// Distributed under the MIT License.
//==============================================================================================================================

//
// Configuration:
//      FILAMENT_SPLIT_EASU:
//              if defined, only do the fast path (early exit) code, and write 1.0 to the
//              depth buffer for the slow path
//
//      FILAMENT_FSR_DERINGING:
//              if defined, performs deringing code
//

#define rcp(x) (1.0/(x))
#define rsqrt inversesqrt

AF3 FsrEasuSampleF(highp AF2 p);

void FsrEasuTapF2(
        inout AF2 aCR,inout AF2 aCG,inout AF2 aCB,
        inout AF2 aW,
        AF2 offX,AF2 offY,
        AF2 dir,
        AF2 len,
        AF1 lob,
        AF1 clp,
        AF2 cR,AF2 cG,AF2 cB) {
    AF2 vX,vY;
    vX=offX*  dir.xx +offY*dir.yy;
    vY=offX*(-dir.yy)+offY*dir.xx;
    vX*=len.x;vY*=len.y;
    AF2 d2=vX*vX+vY*vY;
    d2=min(d2,AF2_(clp));
    AF2 wB=AF2_(2.0/5.0)*d2+AF2_(-1.0);
    AF2 wA=AF2_(lob)*d2+AF2_(-1.0);
    wB*=wB;
    wA*=wA;
    wB=AF2_(25.0/16.0)*wB+AF2_(-(25.0/16.0-1.0));
    AF2 w=wB*wA;
    aCR+=cR*w;aCG+=cG*w;aCB+=cB*w;aW+=w;
}

void FsrEasuL(
        out AF3 pix,
        highp AF2 ip,
        highp AF4 con0,
        highp AF4 con1,
        highp AF4 con2,
        highp AF4 con3){
    highp AF2 pp = ip * con0.xy + con0.zw;
    highp AF2 base = floor(pp);
    highp AF2 f = pp - base;
    highp AF2 h = f * f * (AF2_(3.0) - AF2_(2.0) * f);
    highp AF2 q = f + AF2_(0.25) * (h - f);
    highp AF2 tc = (base + q + AF2_(0.5)) * con1.xy;
    AF3 sC = FsrEasuSampleF(tc);

    AF1 lC = sC.r * AF1_(0.5) + sC.g;
    AF1 dx = dFdx(lC);
    AF1 dy = dFdy(lC);
    highp AF2 gradientScale = AF2_(2.0) / con0.xy;
    AF1 gx = dx * gradientScale.x;
    AF1 gy = dy * gradientScale.y;

    if (gx * gx + gy * gy < AF1_(1.0 / 64.0)) {
        pix = sC;
#ifdef FILAMENT_SPLIT_EASU
        gl_FragDepth = 0.0;
#endif
        return;
    }
#ifdef FILAMENT_SPLIT_EASU
    gl_FragDepth = 1.0;
#else
    highp AF2 offset = abs(gx) >= abs(gy)
            ? AF2(con1.x, 0.0)
            : AF2(0.0, con1.y);
    AF3 sMinus = FsrEasuSampleF(tc - offset);
    AF3 sPlus = FsrEasuSampleF(tc + offset);

    AF3 curvature = AF3_(2.0) * sC - sMinus - sPlus;
    AF3 candidate = sC + AF3_(0.16) * curvature;
    AF3 lo = min(sC, min(sMinus, sPlus));
    AF3 hi = max(sC, max(sMinus, sPlus));

#ifdef FILAMENT_FSR_DERINGING
    AF3 min4 = lo;
    AF3 max4 = hi;
#endif

    candidate = clamp(candidate,
            sC - AF3_(0.25) * (sC - lo),
            sC + AF3_(0.25) * (hi - sC));

#ifdef FILAMENT_FSR_DERINGING
    pix = min(max4, max(min4, candidate));
#else
    pix = candidate;
#endif

#endif // FILAMENT_SPLIT_EASU
}
