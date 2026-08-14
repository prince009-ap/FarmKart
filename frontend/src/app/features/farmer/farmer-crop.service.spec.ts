import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { FarmerCropService } from './farmer-crop.service';
import { FarmerCrop } from '../../core/models/farmer-crop.models';
import { environment } from '../../../environments/environment';

describe('FarmerCropService', () => {
  let service: FarmerCropService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        FarmerCropService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(FarmerCropService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('1. should fetch farmer crops with images', () => {
    const mockCrops: FarmerCrop[] = [
      {
        id: 'crop-1',
        farmerProfileId: 'f-1',
        farmerName: 'Farmer Joe',
        cropName: 'Wheat',
        cropType: 'Cereal',
        area: 5,
        areaUnit: 'Bigha',
        unit: 'Kg',
        quantity: 0,
        status: 'Growing',
        primaryImageUrl: '/uploads/crops/primary.jpg',
        images: [{ id: 'img-1', cropId: 'crop-1', imageUrl: '/uploads/crops/primary.jpg', isPrimary: true, displayOrder: 1, createdAtUtc: '' }],
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString()
      }
    ];

    service.getCrops().subscribe((crops) => {
      expect(crops.length).toBe(1);
      expect(crops[0].primaryImageUrl).toBe(service.resolveImageUrl('/uploads/crops/primary.jpg'));
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/farmer/crops`);
    expect(req.request.method).toBe('GET');
    req.flush(mockCrops);
  });

  it('2. should fetch crop by id', () => {
    const mockCrop: FarmerCrop = {
      id: 'crop-1',
      farmerProfileId: 'f-1',
      farmerName: 'Farmer Joe',
      cropName: 'Wheat',
      cropType: 'Cereal',
      area: 5,
      areaUnit: 'Bigha',
      unit: 'Kg',
      quantity: 0,
      status: 'Growing',
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString()
    };

    service.getCropById('crop-1').subscribe((crop) => {
      expect(crop.id).toBe('crop-1');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/farmer/crops/crop-1`);
    expect(req.request.method).toBe('GET');
    req.flush(mockCrop);
  });

  it('3. should upload crop image', () => {
    const dummyFile = new File(['dummy'], 'test.jpg', { type: 'image/jpeg' });
    service.uploadCropImage('crop-1', dummyFile, true).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/farmer/crops/crop-1/images`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'img-1', imageUrl: '/uploads/crops/test.jpg' });
  });

  it('4. should delete crop image', () => {
    service.deleteCropImage('crop-1', 'img-1').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/farmer/crops/crop-1/images/img-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('5. should set primary crop image', () => {
    service.setPrimaryCropImage('crop-1', 'img-2').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/farmer/crops/crop-1/images/img-2/primary`);
    expect(req.request.method).toBe('PUT');
    req.flush({});
  });
});
