namespace Difframe
{
    public static class AspectRatioCalculator
    {
        public static (int X, int Y) CalculateAspectRatio(int inWidth, int inHeight)
        {
            var lcf = 2;
            bool x_odd, y_odd;
            x_odd = y_odd = false;

            if(inWidth % 3 == 0)
            {
                x_odd = true;
            }

            if(inHeight % 3 == 0)
            {
                y_odd = true;
            }

            if(x_odd && y_odd)
            {
                lcf = 3;
            }

            int num, hcf;
            num = hcf = 1;
            while(inWidth / (num * lcf) >= 1)
            {
                var factor = num * lcf;
                if((inWidth % factor == 0) && inHeight % factor == 0)
                {
                    hcf = factor;
                }
                num += 1;
            }

            return (inWidth / hcf, inHeight / hcf);
        }
    }
}
