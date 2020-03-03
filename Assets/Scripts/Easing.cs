using System;
public class Easing
{
    /// <summary>
    /// Enumeration of all easing equations.
    /// </summary>
    public enum Ease
    {
        Linear,
        QuadOut,
        QuadIn,
        QuadInOut,
        QuadOutIn,
        ExpoOut,
        ExpoIn,
        ExpoInOut,
        ExpoOutIn,
        CubicOut,
        CubicIn,
        CubicInOut,
        CubicOutIn,
        QuartOut,
        QuartIn,
        QuartInOut,
        QuartOutIn,
        QuintOut,
        QuintIn,
        QuintInOut,
        QuintOutIn,
        CircOut,
        CircIn,
        CircInOut,
        CircOutIn,
        SineOut,
        SineIn,
        SineInOut,
        SineOutIn,
        ElasticOut,
        ElasticIn,
        ElasticInOut,
        ElasticOutIn,
        BounceOut,
        BounceIn,
        BounceInOut,
        BounceOutIn,
        BackOut,
        BackIn,
        BackInOut,
        BackOutIn
    }

    #region Linear

    /// <summary>
    /// Easing equation function for a simple linear tweening, with no easing.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double Linear(double t, double b, double c, double d)
    {
        return c * t / d + b;
    }
    public static float Linear(float t, float b, float c, float d)
    {
        return c * t / d + b;
    }

    #endregion

    #region Expo

    /// <summary>
    /// Easing equation function for an exponential (2^t) easing out: 
    /// decelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double ExpoOut(double t, double b, double c, double d)
    {
        return (t == d) ? b + c : c * (-Math.Pow(2, -10 * t / d) + 1) + b;
    }
    public static float ExpoOut(float t, float b, float c, float d)
    {
        return (t == d) ? b + c : c * ((float)-Math.Pow(2.0, -10f * (t / d)) + 1f) + b;
    }

    /// <summary>
    /// Easing equation function for an exponential (2^t) easing in: 
    /// accelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double ExpoIn(double t, double b, double c, double d)
    {
        return (t == 0) ? b : c * Math.Pow(2, 10 * (t / d - 1)) + b;
    }
    public static float ExpoIn(float t, float b, float c, float d)
    {
        return (t == 0) ? b : c * (float)Math.Pow(2.0, 10f * (t / d - 1f)) + b;
    }

    /// <summary>
    /// Easing equation function for an exponential (2^t) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double ExpoInOut(double t, double b, double c, double d)
    {
        if (t == 0)
            return b;

        if (t == d)
            return b + c;

        if ((t /= d / 2) < 1)
            return c / 2 * Math.Pow(2, 10 * (t - 1)) + b;

        return c / 2 * (-Math.Pow(2, -10 * --t) + 2) + b;
    }
    public static float ExpoInOut(float t, float b, float c, float d)
    {
        if (t == 0)
            return b;

        if (t == d)
            return b + c;

        if ((t /= d / 2) < 1)
            return c * 0.5f * (float)Math.Pow(2.0, 10f * (t - 1f)) + b;

        return c * 0.5f * ((float)-Math.Pow(2.0, -10f * (t - 1f)) + 2f) + b;
    }

    /// <summary>
    /// Easing equation function for an exponential (2^t) easing out/in: 
    /// deceleration until halfway, then acceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double ExpoOutIn(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return ExpoOut(t * 2, b, c / 2, d);

        return ExpoIn((t * 2) - d, b + c / 2, c / 2, d);
    }
    public static float ExpoOutIn(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return ExpoOut(t * 2f, b, c * 0.5f, d);

        return ExpoIn((t * 2f) - d, b + c * 0.5f, c * 0.5f, d);
    }

    #endregion

    #region Circular

    /// <summary>
    /// Easing equation function for a circular (sqrt(1-t^2)) easing out: 
    /// decelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double CircOut(double t, double b, double c, double d)
    {
        return c * Math.Sqrt(1 - (t = t / d - 1) * t) + b;
    }
    public static float CircOut(float t, float b, float c, float d)
    {
        return c * (float)Math.Sqrt(1f - (t = t / d - 1f) * t) + b;
    }

    /// <summary>
    /// Easing equation function for a circular (sqrt(1-t^2)) easing in: 
    /// accelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double CircIn(double t, double b, double c, double d)
    {
        return -c * (Math.Sqrt(1 - (t /= d) * t) - 1) + b;
    }
    public static float CircIn(float t, float b, float c, float d)
    {
        return -c * ((float)Math.Sqrt(1f - (t /= d) * t) - 1f) + b;
    }

    /// <summary>
    /// Easing equation function for a circular (sqrt(1-t^2)) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double CircInOut(double t, double b, double c, double d)
    {
        if ((t /= d / 2) < 1)
            return -c / 2 * (Math.Sqrt(1 - t * t) - 1) + b;

        return c / 2 * (Math.Sqrt(1 - (t -= 2) * t) + 1) + b;
    }
    public static float CircInOut(float t, float b, float c, float d)
    {
        if ((t /= d * 0.5f) < 1f)
            return -c * 0.5f * ((float)Math.Sqrt(1f - t * t) - 1f) + b;

        return c * 0.5f * ((float)Math.Sqrt(1f - (t -= 2f) * t) + 1f) + b;
    }

    /// <summary>
    /// Easing equation function for a circular (sqrt(1-t^2)) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double CircOutIn(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return CircOut(t * 2, b, c / 2, d);

        return CircIn((t * 2) - d, b + c / 2, c / 2, d);
    }
    public static float CircOutIn(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return CircOut(t * 2f, b, c * 0.5f, d);

        return CircIn((t * 2f) - d, b + c * 0.5f, c * 0.5f, d);
    }

    #endregion

    #region Quad

    /// <summary>
    /// Easing equation function for a quadratic (t^2) easing out: 
    /// decelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuadOut(double t, double b, double c, double d)
    {
        return -c * (t /= d) * (t - 2) + b;
    }
    public static float QuadOut(float t, float b, float c, float d)
    {
        return -c * (t /= d) * (t - 2f) + b;
    }

    /// <summary>
    /// Easing equation function for a quadratic (t^2) easing in: 
    /// accelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuadIn(double t, double b, double c, double d)
    {
        return c * (t /= d) * t + b;
    }
    public static float QuadIn(float t, float b, float c, float d)
    {
        return c * (t /= d) * t + b;
    }

    /// <summary>
    /// Easing equation function for a quadratic (t^2) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuadInOut(double t, double b, double c, double d)
    {
        if ((t /= d / 2) < 1)
            return c / 2 * t * t + b;

        return -c / 2 * ((--t) * (t - 2) - 1) + b;
    }
    public static float QuadInOut(float t, float b, float c, float d)
    {
        if ((t /= d * 0.5f) < 1f)
            return c * 0.5f * t * t + b;

        return -c * 0.5f * ((--t) * (t - 2f) - 1f) + b;
    }

    /// <summary>
    /// Easing equation function for a quadratic (t^2) easing out/in: 
    /// deceleration until halfway, then acceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuadOutIn(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return QuadOut(t * 2, b, c / 2, d);

        return QuadIn((t * 2) - d, b + c / 2, c / 2, d);
    }
    public static float QuadOutIn(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return QuadOut(t * 2f, b, c * 0.5f, d);

        return QuadIn((t * 2f) - d, b + c * 0.5f, c * 0.5f, d);
    }

    #endregion

    #region Sine

    /// <summary>
    /// Easing equation function for a sinusoidal (sin(t)) easing out: 
    /// decelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double SineOut(double t, double b, double c, double d)
    {
        return c * Math.Sin(t / d * (Math.PI / 2)) + b;
    }
    public static float SineOut(float t, float b, float c, float d)
    {
        return c * (float)Math.Sin((t / d) * (Math.PI / 2.0)) + b;
    }

    /// <summary>
    /// Easing equation function for a sinusoidal (sin(t)) easing in: 
    /// accelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double SineIn(double t, double b, double c, double d)
    {
        return -c * Math.Cos(t / d * (Math.PI / 2)) + c + b;
    }
    public static float SineIn(float t, float b, float c, float d)
    {
        return -c * (float)Math.Cos((t / d) * (Math.PI / 2.0)) + c + b;
    }

    /// <summary>
    /// Easing equation function for a sinusoidal (sin(t)) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double SineInOut(double t, double b, double c, double d)
    {
        if ((t /= d / 2) < 1)
            return c / 2 * (Math.Sin(Math.PI * t / 2)) + b;

        return -c / 2 * (Math.Cos(Math.PI * --t / 2) - 2) + b;
    }
    public static float SineInOut(float t, float b, float c, float d)
    {
        if ((t /= d * 0.5f) < 1f)
            return c * 0.5f * ((float)Math.Sin(Math.PI * (t * 0.5f))) + b;

        return -c * 0.5f * ((float)Math.Cos(Math.PI * (--t * 0.5f)) - 2f) + b;
    }

    /// <summary>
    /// Easing equation function for a sinusoidal (sin(t)) easing in/out: 
    /// deceleration until halfway, then acceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double SineOutIn(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return SineOut(t * 2, b, c / 2, d);

        return SineIn((t * 2) - d, b + c / 2, c / 2, d);
    }
    public static float SineOutIn(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return SineOut(t * 2f, b, c * 0.5f, d);

        return SineIn((t * 2f) - d, b + c * 0.5f, c * 0.5f, d);
    }

    #endregion

    #region Cubic

    /// <summary>
    /// Easing equation function for a cubic (t^3) easing out: 
    /// decelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double CubicOut(double t, double b, double c, double d)
    {
        return c * ((t = t / d - 1) * t * t + 1) + b;
    }
    public static float CubicOut(float t, float b, float c, float d)
    {
        return c * ((t = t / d - 1f) * t * t + 1f) + b;
    }

    /// <summary>
    /// Easing equation function for a cubic (t^3) easing in: 
    /// accelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double CubicIn(double t, double b, double c, double d)
    {
        return c * (t /= d) * t * t + b;
    }
    public static float CubicIn(float t, float b, float c, float d)
    {
        return c * (t /= d) * t * t + b;
    }

    /// <summary>
    /// Easing equation function for a cubic (t^3) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double CubicInOut(double t, double b, double c, double d)
    {
        if ((t /= d / 2) < 1)
            return c / 2 * t * t * t + b;

        return c / 2 * ((t -= 2) * t * t + 2) + b;
    }
    public static float CubicInOut(float t, float b, float c, float d)
    {
        if ((t /= d * 0.5f) < 1f)
            return c * 0.5f * t * t * t + b;

        return c * 0.5f * ((t -= 2f) * t * t + 2f) + b;
    }

    /// <summary>
    /// Easing equation function for a cubic (t^3) easing out/in: 
    /// deceleration until halfway, then acceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double CubicOutIn(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return CubicOut(t * 2, b, c / 2, d);

        return CubicIn((t * 2) - d, b + c / 2, c / 2, d);
    }
    public static float CubicOutIn(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return CubicOut(t * 2f, b, c * 0.5f, d);

        return CubicIn((t * 2f) - d, b + c * 0.5f, c * 0.5f, d);
    }

    #endregion

    #region Quartic

    /// <summary>
    /// Easing equation function for a quartic (t^4) easing out: 
    /// decelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuartOut(double t, double b, double c, double d)
    {
        return -c * ((t = t / d - 1) * t * t * t - 1) + b;
    }
    public static float QuartOut(float t, float b, float c, float d)
    {
        return -c * ((t = t / d - 1f) * t * t * t - 1f) + b;
    }

    /// <summary>
    /// Easing equation function for a quartic (t^4) easing in: 
    /// accelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuartIn(double t, double b, double c, double d)
    {
        return c * (t /= d) * t * t * t + b;
    }
    public static float QuartIn(float t, float b, float c, float d)
    {
        return c * (t /= d) * t * t * t + b;
    }

    /// <summary>
    /// Easing equation function for a quartic (t^4) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuartInOut(double t, double b, double c, double d)
    {
        if ((t /= d / 2) < 1)
            return c / 2 * t * t * t * t + b;

        return -c / 2 * ((t -= 2) * t * t * t - 2) + b;
    }
    public static float QuartInOut(float t, float b, float c, float d)
    {
        if ((t /= d * 0.5f) < 1f)
            return c * 0.5f * t * t * t * t + b;

        return -c * 0.5f * ((t -= 2f) * t * t * t - 2f) + b;
    }

    /// <summary>
    /// Easing equation function for a quartic (t^4) easing out/in: 
    /// deceleration until halfway, then acceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuartOutIn(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return QuartOut(t * 2, b, c / 2, d);

        return QuartIn((t * 2) - d, b + c / 2, c / 2, d);
    }
    public static float QuartOutIn(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return QuartOut(t * 2f, b, c * 0.5f, d);
        return QuartIn((t * 2f) - d, b + c * 0.5f, c * 0.5f, d);
    }

    #endregion

    #region Quintic

    /// <summary>
    /// Easing equation function for a quintic (t^5) easing out: 
    /// decelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuintOut(double t, double b, double c, double d)
    {
        return c * ((t = t / d - 1) * t * t * t * t + 1) + b;
    }
    public static float QuintOut(float t, float b, float c, float d)
    {
        return c * ((t = t / d - 1f) * t * t * t * t + 1f) + b;
    }

    /// <summary>
    /// Easing equation function for a quintic (t^5) easing in: 
    /// accelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuintIn(double t, double b, double c, double d)
    {
        return c * (t /= d) * t * t * t * t + b;
    }
    public static float QuintIn(float t, float b, float c, float d)
    {
        return c * (t /= d) * t * t * t * t + b;
    }

    /// <summary>
    /// Easing equation function for a quintic (t^5) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuintInOut(double t, double b, double c, double d)
    {
        if ((t /= d / 2) < 1)
            return c / 2 * t * t * t * t * t + b;
        return c / 2 * ((t -= 2) * t * t * t * t + 2) + b;
    }
    public static float QuintInOut(float t, float b, float c, float d)
    {
        if ((t /= d * 0.5f) < 1f)
            return c * 0.5f * t * t * t * t * t + b;
        return c * 0.5f * ((t -= 2f) * t * t * t * t + 2f) + b;
    }

    /// <summary>
    /// Easing equation function for a quintic (t^5) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double QuintOutIn(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return QuintOut(t * 2, b, c / 2, d);
        return QuintIn((t * 2) - d, b + c / 2, c / 2, d);
    }
    public static float QuintOutIn(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return QuintOut(t * 2f, b, c * 0.5f, d);
        return QuintIn((t * 2f) - d, b + c * 0.5f, c * 0.5f, d);
    }

    #endregion

    #region Elastic

    /// <summary>
    /// Easing equation function for an elastic (exponentially decaying sine wave) easing out: 
    /// decelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double ElasticOut(double t, double b, double c, double d)
    {
        if ((t /= d) == 1)
            return b + c;

        double p = d * .3;
        double s = p / 4;

        return (c * Math.Pow(2, -10 * t) * Math.Sin((t * d - s) * (2 * Math.PI) / p) + c + b);
    }
    public static float ElasticOut(float t, float b, float c, float d)
    {
        if ((t /= d) == 1f)
            return b + c;

        float p = d * .3f;
        float s = p * 0.25f;

        return (c * (float)Math.Pow(2.0, -10f * t) * (float)Math.Sin((t * d - s) * (2.0 * Math.PI) / p) + c + b);
    }

    /// <summary>
    /// Easing equation function for an elastic (exponentially decaying sine wave) easing in: 
    /// accelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double ElasticIn(double t, double b, double c, double d)
    {
        if ((t /= d) == 1)
            return b + c;

        double p = d * .3;
        double s = p / 4;

        return -(c * Math.Pow(2, 10 * (t -= 1)) * Math.Sin((t * d - s) * (2 * Math.PI) / p)) + b;
    }
    public static float ElasticIn(float t, float b, float c, float d)
    {
        if ((t /= d) == 1f)
            return b + c;

        float p = d * .3f;
        float s = p * 0.25f;

        return -(c * (float)Math.Pow(2.0, 10f * (t -= 1f)) * (float)Math.Sin((t * d - s) * (2.0 * Math.PI) / p)) + b;
    }

    /// <summary>
    /// Easing equation function for an elastic (exponentially decaying sine wave) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double ElasticInOut(double t, double b, double c, double d)
    {
        if ((t /= d / 2) == 2)
            return b + c;

        double p = d * (.3 * 1.5);
        double s = p / 4;

        if (t < 1)
            return -.5 * (c * Math.Pow(2, 10 * (t -= 1)) * Math.Sin((t * d - s) * (2 * Math.PI) / p)) + b;
        return c * Math.Pow(2, -10 * (t -= 1)) * Math.Sin((t * d - s) * (2 * Math.PI) / p) * .5 + c + b;
    }
    public static float ElasticInOut(float t, float b, float c, float d)
    {
        if ((t /= d / 2) == 2)
            return b + c;

        float p = d * (.3f * 1.5f);
        float s = p * 0.25f;

        if (t < 1f)
            return -.5f * (c * (float)Math.Pow(2.0, 10f * (t -= 1f)) * (float)Math.Sin((t * d - s) * (2.0 * Math.PI) / p)) + b;
        return c * (float)Math.Pow(2.0, -10f * (t -= 1f)) * (float)Math.Sin((t * d - s) * (2.0 * Math.PI) / p) * .5f + c + b;
    }

    /// <summary>
    /// Easing equation function for an elastic (exponentially decaying sine wave) easing out/in: 
    /// deceleration until halfway, then acceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double ElasticOutIn(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return ElasticOut(t * 2, b, c / 2, d);
        return ElasticIn((t * 2) - d, b + c / 2, c / 2, d);
    }
    public static float ElasticOutIn(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return ElasticOut(t * 2f, b, c * 0.5f, d);
        return ElasticIn((t * 2f) - d, b + c * 0.5f, c * 0.5f, d);
    }

    #endregion

    #region Bounce

    /// <summary>
    /// Easing equation function for a bounce (exponentially decaying parabolic bounce) easing out: 
    /// decelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double BounceOut(double t, double b, double c, double d)
    {
        if ((t /= d) < (1 / 2.75))
            return c * (7.5625 * t * t) + b;
        else if (t < (2 / 2.75))
            return c * (7.5625 * (t -= (1.5 / 2.75)) * t + .75) + b;
        else if (t < (2.5 / 2.75))
            return c * (7.5625 * (t -= (2.25 / 2.75)) * t + .9375) + b;
        else
            return c * (7.5625 * (t -= (2.625 / 2.75)) * t + .984375) + b;
    }
    public static float BounceOut(float t, float b, float c, float d)
    {
        if ((t /= d) < (1 / 2.75f))
            return c * (7.5625f * t * t) + b;
        else if (t < (2f / 2.75f))
            return c * (7.5625f * (t -= (1.5f / 2.75f)) * t + .75f) + b;
        else if (t < (2.5f / 2.75f))
            return c * (7.5625f * (t -= (2.25f / 2.75f)) * t + .9375f) + b;
        else
            return c * (7.5625f * (t -= (2.625f / 2.75f)) * t + .984375f) + b;
    }

    /// <summary>
    /// Easing equation function for a bounce (exponentially decaying parabolic bounce) easing in: 
    /// accelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double BounceIn(double t, double b, double c, double d)
    {
        return c - BounceOut(d - t, 0, c, d) + b;
    }
    public static float BounceIn(float t, float b, float c, float d)
    {
        return c - BounceOut(d - t, 0, c, d) + b;
    }

    /// <summary>
    /// Easing equation function for a bounce (exponentially decaying parabolic bounce) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double BounceInOut(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return BounceIn(t * 2, 0, c, d) * .5 + b;
        else
            return BounceOut(t * 2 - d, 0, c, d) * .5 + c * .5 + b;
    }
    public static float BounceInOut(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return BounceIn(t * 2f, 0, c, d) * .5f + b;
        else
            return BounceOut(t * 2f - d, 0, c, d) * .5f + c * .5f + b;
    }

    /// <summary>
    /// Easing equation function for a bounce (exponentially decaying parabolic bounce) easing out/in: 
    /// deceleration until halfway, then acceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double BounceOutIn(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return BounceOut(t * 2, b, c / 2, d);
        return BounceIn((t * 2) - d, b + c / 2, c / 2, d);
    }
    public static float BounceOutIn(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return BounceOut(t * 2f, b, c * 0.5f, d);
        return BounceIn((t * 2f) - d, b + c * 0.5f, c * 0.5f, d);
    }

    #endregion

    #region Back

    /// <summary>
    /// Easing equation function for a back (overshooting cubic easing: (s+1)*t^3 - s*t^2) easing out: 
    /// decelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double BackOut(double t, double b, double c, double d)
    {
        return c * ((t = t / d - 1) * t * ((1.70158 + 1) * t + 1.70158) + 1) + b;
    }
    public static float BackOut(float t, float b, float c, float d)
    {
        return c * ((t = t / d - 1f) * t * ((1.70158f + 1f) * t + 1.70158f) + 1f) + b;
    }

    /// <summary>
    /// Easing equation function for a back (overshooting cubic easing: (s+1)*t^3 - s*t^2) easing in: 
    /// accelerating from zero velocity.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double BackIn(double t, double b, double c, double d)
    {
        return c * (t /= d) * t * ((1.70158 + 1) * t - 1.70158) + b;
    }
    public static float BackIn(float t, float b, float c, float d)
    {
        return c * (t /= d) * t * ((1.70158f + 1f) * t - 1.70158f) + b;
    }

    /// <summary>
    /// Easing equation function for a back (overshooting cubic easing: (s+1)*t^3 - s*t^2) easing in/out: 
    /// acceleration until halfway, then deceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double BackInOut(double t, double b, double c, double d)
    {
        double s = 1.70158;
        if ((t /= d / 2) < 1)
            return c / 2 * (t * t * (((s *= (1.525)) + 1) * t - s)) + b;
        return c / 2 * ((t -= 2) * t * (((s *= (1.525)) + 1) * t + s) + 2) + b;
    }
    public static float BackInOut(float t, float b, float c, float d)
    {
        float s = 1.70158f;
        if ((t /= d * 0.5f) < 1f)
            return c * 0.5f * (t * t * (((s *= (1.525f)) + 1f) * t - s)) + b;
        return c * 0.5f * ((t -= 2f) * t * (((s *= (1.525f)) + 1f) * t + s) + 2) + b;
    }

    /// <summary>
    /// Easing equation function for a back (overshooting cubic easing: (s+1)*t^3 - s*t^2) easing out/in: 
    /// deceleration until halfway, then acceleration.
    /// </summary>
    /// <param name="t">Current time in seconds.</param>
    /// <param name="b">Starting value.</param>
    /// <param name="c">Final value.</param>
    /// <param name="d">Duration of animation.</param>
    /// <returns>The correct value.</returns>
    public static double BackOutIn(double t, double b, double c, double d)
    {
        if (t < d / 2)
            return BackOut(t * 2, b, c / 2, d);
        return BackIn((t * 2) - d, b + c / 2, c / 2, d);
    }
    public static float BackOutIn(float t, float b, float c, float d)
    {
        if (t < d * 0.5f)
            return BackOut(t * 2f, b, c * 0.5f, d);
        return BackIn((t * 2f) - d, b + c * 0.5f, c * 0.5f, d);
    }

    #endregion
}