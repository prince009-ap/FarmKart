import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FarmerCropsComponent } from './farmer-crops.component';
import { FarmerCropService } from './farmer-crop.service';
import { of } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';

describe('FarmerCropsComponent', () => {
  let component: FarmerCropsComponent;
  let fixture: ComponentFixture<FarmerCropsComponent>;
  let mockCropService: any;

  beforeEach(async () => {
    mockCropService = {
      getCrops: vi.fn().mockReturnValue(of([])),
      deleteCrop: vi.fn().mockReturnValue(of(void 0))
    };

    await TestBed.configureTestingModule({
      imports: [FarmerCropsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        provideRouter([]),
        { provide: FarmerCropService, useValue: mockCropService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerCropsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('1. My Crops page loads', () => {
    expect(component).toBeTruthy();
    expect(mockCropService.getCrops).toHaveBeenCalled();
  });

  it('2. Empty state works when no crops returned', () => {
    expect(component.crops().length).toBe(0);
    expect(component.filteredCrops.length).toBe(0);
  });

  it('3. Crop list displays with primary image thumbnail', () => {
    component.crops.set([
      {
        id: 'c-1',
        farmerProfileId: 'f-1',
        farmerName: 'Farmer',
        cropName: 'Wheat Special',
        cropType: 'Cereal',
        area: 5,
        areaUnit: 'Bigha',
        quantity: 0,
        unit: 'Kg',
        status: 'Growing',
        primaryImageUrl: '/uploads/crops/primary.jpg',
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString()
      }
    ]);

    expect(component.filteredCrops.length).toBe(1);
    expect(component.filteredCrops[0].primaryImageUrl).toBe('/uploads/crops/primary.jpg');
  });

  it('4. Search filtering works', () => {
    component.crops.set([
      { id: 'c-1', farmerProfileId: 'f-1', farmerName: 'Farmer', cropName: 'Wheat Special', cropType: 'Cereal', area: 5, areaUnit: 'Bigha', quantity: 0, unit: 'Kg', status: 'Growing', createdAtUtc: '', updatedAtUtc: '' },
      { id: 'c-2', farmerProfileId: 'f-1', farmerName: 'Farmer', cropName: 'Cotton Commercial', cropType: 'Commercial', area: 10, areaUnit: 'Acre', quantity: 0, unit: 'Kg', status: 'Planned', createdAtUtc: '', updatedAtUtc: '' }
    ]);

    component.searchTerm.set('Wheat');
    expect(component.filteredCrops.length).toBe(1);
    expect(component.filteredCrops[0].cropName).toBe('Wheat Special');
  });

  it('5. Delete confirmation modal opens and confirms delete', () => {
    const mockCrop = { id: 'c-1', farmerProfileId: 'f-1', farmerName: 'Farmer', cropName: 'Wheat', cropType: 'Cereal', area: 5, areaUnit: 'Bigha', quantity: 0, unit: 'Kg', status: 'Growing', createdAtUtc: '', updatedAtUtc: '' };
    component.crops.set([mockCrop]);

    component.openDeleteModal(mockCrop);
    expect(component.cropToDelete()).toEqual(mockCrop);

    component.confirmDelete();
    expect(mockCropService.deleteCrop).toHaveBeenCalledWith('c-1');
  });

  it('6. Crop card displays the API-formatted stock without converting it again', () => {
    component.crops.set([{
      id: 'c-1', farmerProfileId: 'f-1', farmerName: 'Farmer', cropName: 'Wheat', cropType: 'Cereal',
      area: 5, areaUnit: 'Bigha', quantity: 500, unit: 'Kg', status: 'Harvested',
      availableQuantityKg: 500, availableQuantityFormatted: '5 Quintals',
      createdAtUtc: '', updatedAtUtc: ''
    }]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('5 Quintals');
    expect(fixture.nativeElement.textContent).not.toContain('1 Ton');
  });
});
