/*
author: marco
project: mzdb project
*/

#ifndef __PEAKFINDER__
#define __PEAKFINDER__


#include <string>
#include <stdint.h>
#include <stdio.h>
#include <exception>


#include "../utils/mzUtils.hpp"
#include "peak_picking/mzPeakFinderZeroBounded.hpp"
#include "peak_picking/mzPeakFinderWavelet.hpp"

namespace mzdb {
using namespace std;

enum PeakPickingAlgorithm{
    GENERIC = 1,
    THERMO = 2
};


//Deprecated, not used anymore
/*struct PeakFinderResults {
    int nbPeaks;
    double maxmz;
    double minmz;
    DataMode effectiveMode;
    PeakPickingAlgorithm peakPickingMode;
};*/

/**
Does not handle the deletion of the peakFinder
*/
class PWIZ_API_DECL mzPeakFinderProxy {

private:
    //PeakFinderResults* peakFinderResults;

    template<class mz_t, class int_t>
    static void computeCentroids(const pwiz::msdata::SpectrumPtr &s,
                                 vector<std::shared_ptr<Centroid<mz_t, int_t> > >& results) {
        /*turn into centroids objects */
        vector<pwiz::msdata::MZIntensityPair> pairs;
        s->getMZIntensityPairs(pairs);

        //---following lines remove all zeros of the intensity array
        /*pairs.erase(std::remove_if(pairs.begin(), pairs.end(), [](const pwiz::msdata::MZIntensityPair& p) { return p.intensity == (int_t)0.0; }),
                    pairs.end());*/

        results.resize(pairs.size());
        float rt = static_cast<float>(s->scanList.scans[0].cvParam(pwiz::msdata::MS_scan_start_time).timeInSeconds());
        std::transform(pairs.begin(), pairs.end(), results.begin(), [&rt, s](pwiz::msdata::MZIntensityPair& p) -> std::shared_ptr<Centroid<mz_t, int_t> > {
                mz_t mz = (mz_t)p.mz;
                int_t ints = (int_t)p.intensity;
                return std::make_shared<Centroid<mz_t, int_t> >(mz, ints, rt);
        });
    }

public:
    static int counter;

    inline mzPeakFinderProxy() {}


    template<class mz_t, class int_t>
    static DataMode computePeaks(const pwiz::msdata::SpectrumPtr &s,
                                 vector<std::shared_ptr<Centroid<mz_t, int_t> > >& results,
                                 DataMode wantedMode,
                                 pwiz::msdata::CVID fileType,
                                 mzPeakFinderUtils::PeakPickerParams& peakPickerParams) {

        const pwiz::msdata::CVParam& isCentroided = s->cvParam(pwiz::msdata::MS_centroid_spectrum);
        DataMode currentMode = ( isCentroided.empty() ) ? PROFILE: CENTROID;

        DataMode effectiveMode;


        if (wantedMode == PROFILE && currentMode == PROFILE) {
            effectiveMode = PROFILE;
            computeCentroids<mz_t, int_t>(s, results);

        } else if ((wantedMode == CENTROID && currentMode == PROFILE) || (wantedMode == FITTED && currentMode == PROFILE)) {//findPeak then centroidize}
            effectiveMode = wantedMode;
            findPeaks<mz_t, int_t>(s, results, fileType, peakPickerParams);

        } else { // current is CENTROID nothing to do
            effectiveMode = CENTROID;
            computeCentroids<mz_t, int_t>(s, results);

        }
        //printf("currentMode:%d, wantedMode:%d, effectiveMode:%d\n", (int)currentMode, (int)wantedMode, (int)effectiveMode);
        return effectiveMode;
    }

    /**
     *
     */
    template<class mz_t, class int_t>
    static void findPeaks(const pwiz::msdata::SpectrumPtr &s,
                          vector<std::shared_ptr<Centroid<mz_t, int_t> > >& v,
                          pwiz::msdata::CVID fileType,
                          mzPeakFinderUtils::PeakPickerParams& peakPickerParams ) {


        switch (fileType) {
            case pwiz::msdata::MS_ABI_WIFF_file :{
                peakPickerParams.adaptiveBaselineAndNoise = true;
                peakPickerParams.optimizationOpt = 0x01;
                peakPickerParams.minSNR = 1.5;
                peakPickerParams.fwhm = TOF_FWHM;
                mzPeakFinderWavelet::findPeaks(s, v, peakPickerParams);
                break;
            }
            case pwiz::msdata::MS_Thermo_RAW_file : {
                //all default, ideal case
            //LOG(INFO) << "Thermo function";
                peakPickerParams.adaptiveBaselineAndNoise = false;
                peakPickerParams.noise = 0;
                peakPickerParams.baseline = 0;
                peakPickerParams.minSNR = 0;
                mzPeakFinderZeroBounded::findPeaks<mz_t, int_t>(s, v, peakPickerParams);
                break;
            }
        }
    }

    //deprecated
    //inline PeakFinderResults* getResults() {return peakFinderResults;}
};


} //end namespace
#endif
