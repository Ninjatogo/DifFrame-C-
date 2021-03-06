﻿namespace Difframe
{
    public static class AspectRatioCalculator
    {
        public static (int X, int Y) CalculateAspectRatio(int inWidth, int inHeight)
        {
            //def calculate_aspect_ratio(x_input: object, y_input: object) -> object:
            //    lcf = 2
            //    x_odd = y_odd = False

            //    if x_input % 3 == 0:
            //        x_odd = True

            //    if y_input % 3 == 0:
            //        y_odd = True

            //    if x_odd * y_odd:
            //        lcf = 3

            //    num: int
            //    num = hcf = 1
            //    while (x_input // (num * lcf)) >= 1:
            //        factor = (num * lcf)
            //        if x_input % factor == 0 and y_input % factor == 0:
            //            hcf = factor
            //        num += 1

            //    return x_input // hcf, y_input // hcf

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
