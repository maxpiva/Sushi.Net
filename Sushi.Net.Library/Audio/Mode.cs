namespace Sushi.Net.Library.Audio
{
    public enum Mode
    {
        //
        // Summary:
        //     \f[R(x,y)= \sum _{x',y'} (T(x',y')-I(x+x',y+y'))^2\f]
        SqDiff = 0,
        //
        // Summary:
        //     \f[R(x,y)= \frac{\sum_{x',y'} (T(x',y')-I(x+x',y+y'))^2}{\sqrt{\sum_{x',y'}T(x',y')^2
        //     \cdot \sum_{x',y'} I(x+x',y+y')^2}}\f]
        SqDiffNormed = 1,
        //
        // Summary:
        //     \f[R(x,y)= \sum _{x',y'} (T(x',y') \cdot I(x+x',y+y'))\f]
        CCorr = 2,
        //
        // Summary:
        //     \f[R(x,y)= \frac{\sum_{x',y'} (T(x',y') \cdot I(x+x',y+y'))}{\sqrt{\sum_{x',y'}T(x',y')^2
        //     \cdot \sum_{x',y'} I(x+x',y+y')^2}}\f]
        CCorrNormed = 3,
        //
        // Summary:
        //     \f[R(x,y)= \sum _{x',y'} (T'(x',y') \cdot I'(x+x',y+y'))\f] where \f[\begin{array}{l}
        //     T'(x',y')=T(x',y') - 1/(w \cdot h) \cdot \sum _{x'',y''} T(x'',y'') \\ I'(x+x',y+y')=I(x+x',y+y')
        //     - 1/(w \cdot h) \cdot \sum _{x'',y''} I(x+x'',y+y'') \end{array}\f]
        CCoeff = 4,
        //
        // Summary:
        //     \f[R(x,y)= \frac{ \sum_{x',y'} (T'(x',y') \cdot I'(x+x',y+y')) }{ \sqrt{\sum_{x',y'}T'(x',y')^2
        //     \cdot \sum_{x',y'} I'(x+x',y+y')^2} }\f]
        CCoeffNormed = 5
    }
}