import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FarmerCropDetailComponent } from './farmer-crop-detail.component';
import { FarmerCropService } from './farmer-crop.service';
import { of } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter, ActivatedRoute } from '@angular/router';

describe('FarmerCropDetailComponent', () => {
  let component: FarmerCropDetailComponent;
  let fixture: ComponentFixture<FarmerCropDetailComponent>;
  let mockCropService: any;

  beforeEach(async () => {
    mockCropService = {
      getCropById: vi.fn().mockReturnValue(of({
        id: 'c-1',
        farmerProfileId: 'f-1',
        farmerName: 'Farmer Joe',
        cropName: 'Wheat Details Test',
        cropType: 'Cereal',
        variety: 'GW-322',
        area: 5,
        areaUnit: 'Bigha',
        quantity: 0,
        unit: 'Kg',
        status: 'Growing',
        description: 'Test notes',
        primaryImageUrl: '/uploads/crops/1.jpg',
        images: [
          { id: 'img-1', cropId: 'c-1', imageUrl: '/uploads/crops/1.jpg', isPrimary: true, displayOrder: 1, createdAtUtc: '' },
          { id: 'img-2', cropId: 'c-1', imageUrl: '/uploads/crops/2.jpg', isPrimary: false, displayOrder: 2, createdAtUtc: '' }
        ],
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString()
      })),
      getCropStock: vi.fn().mockReturnValue(of({
        cropId: 'c-1',
        cropName: 'Wheat Details Test',
        cropStatus: 'Growing',
        availableQuantityKg: 0,
        availableQuantityFormatted: '0 Kg',
        displayUnit: 'Kg',
        lastUpdatedUtc: null,
        totalTransactionsCount: 0
      })),
      deleteCrop: vi.fn().mockReturnValue(of(void 0))
    };

    await TestBed.configureTestingModule({
      imports: [FarmerCropDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        provideRouter([{ path: 'farmer/crops', component: FarmerCropDetailComponent }]),
        { provide: FarmerCropService, useValue: mockCropService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => 'c-1' } }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerCropDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('1. Crop details and images load successfully', () => {
    expect(component).toBeTruthy();
    expect(mockCropService.getCropById).toHaveBeenCalledWith('c-1');
    expect(component.crop()?.cropName).toBe('Wheat Details Test');
    expect(component.crop()?.images?.length).toBe(2);
    expect(component.selectedImage()).toBe('/uploads/crops/1.jpg');
  });

  it('2. Main hero image changes when thumbnail is selected', () => {
    component.selectMainImage('/uploads/crops/2.jpg');
    expect(component.selectedImage()).toBe('/uploads/crops/2.jpg');
  });

  it('3. Open delete modal and delete crop', () => {
    component.openDeleteModal();
    expect(component.showDeleteModal()).toBe(true);

    component.confirmDelete();
    expect(mockCropService.deleteCrop).toHaveBeenCalledWith('c-1');
  });
});
