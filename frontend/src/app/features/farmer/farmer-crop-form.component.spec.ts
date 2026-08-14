import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FarmerCropFormComponent } from './farmer-crop-form.component';
import { FarmerCropService } from './farmer-crop.service';
import { of } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter, ActivatedRoute } from '@angular/router';

describe('FarmerCropFormComponent', () => {
  let component: FarmerCropFormComponent;
  let fixture: ComponentFixture<FarmerCropFormComponent>;
  let mockCropService: any;

  beforeEach(async () => {
    mockCropService = {
      createCrop: vi.fn().mockReturnValue(of({ id: 'new-c-1' })),
      updateCrop: vi.fn().mockReturnValue(of({})),
      uploadCropImage: vi.fn().mockReturnValue(of({ id: 'img-1', imageUrl: '/uploads/crops/1.jpg' })),
      deleteCropImage: vi.fn().mockReturnValue(of(void 0)),
      setPrimaryCropImage: vi.fn().mockReturnValue(of({ images: [] })),
      getCropById: vi.fn().mockReturnValue(of({
        id: 'c-1',
        farmerProfileId: 'f-1',
        farmerName: 'Farmer',
        cropName: 'Wheat Existing',
        cropType: 'Cereal',
        area: 5,
        areaUnit: 'Bigha',
        quantity: 0,
        unit: 'Kg',
        status: 'Growing',
        images: [{ id: 'img-1', cropId: 'c-1', imageUrl: '/uploads/crops/1.jpg', isPrimary: true, displayOrder: 1, createdAtUtc: '' }],
        createdAtUtc: '',
        updatedAtUtc: ''
      }))
    };

    await TestBed.configureTestingModule({
      imports: [FarmerCropFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        provideRouter([{ path: 'farmer/crops', component: FarmerCropFormComponent }]),
        { provide: FarmerCropService, useValue: mockCropService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null } }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FarmerCropFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('1. Add Crop form loads in creation mode', () => {
    expect(component).toBeTruthy();
    expect(component.isEditMode()).toBe(false);
  });

  it('2. Invalid file format shows error', () => {
    const invalidFile = new File(['exe'], 'test.exe', { type: 'application/octet-stream' });
    const event = { target: { files: [invalidFile], value: '' } } as any;

    component.onFileSelected(event);
    expect(component.errorMessage()).toContain('Invalid file format');
  });

  it('3. Oversized file shows error', () => {
    const hugeFile = new File([new ArrayBuffer(21 * 1024 * 1024)], 'huge.jpg', { type: 'image/jpeg' });
    const event = { target: { files: [hugeFile], value: '' } } as any;

    component.onFileSelected(event);
    expect(component.errorMessage()).toContain('exceeds the maximum allowed size');
  });

  it('4. Pending image can be removed', () => {
    component.pendingImages.set([
      { file: new File([], '1.jpg', { type: 'image/jpeg' }), previewUrl: 'data:1', isPrimary: true },
      { file: new File([], '2.jpg', { type: 'image/jpeg' }), previewUrl: 'data:2', isPrimary: false }
    ]);

    component.removePendingImage(0);
    expect(component.pendingImages().length).toBe(1);
    expect(component.pendingImages()[0].isPrimary).toBe(true);
  });

  it('5. Pending image can be set as primary', () => {
    component.pendingImages.set([
      { file: new File([], '1.jpg', { type: 'image/jpeg' }), previewUrl: 'data:1', isPrimary: true },
      { file: new File([], '2.jpg', { type: 'image/jpeg' }), previewUrl: 'data:2', isPrimary: false }
    ]);

    component.setPendingPrimary(1);
    expect(component.pendingImages()[0].isPrimary).toBe(false);
    expect(component.pendingImages()[1].isPrimary).toBe(true);
  });
});
