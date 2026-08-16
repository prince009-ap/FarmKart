import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { CustomerMachineryComponent } from './customer-machinery.component';
import { CustomerMachineryDetailComponent } from './customer-machinery-detail.component';
import { CustomerMyRentalsComponent } from './customer-my-rentals.component';
import { MyMachineryFormComponent } from '../farmer/my-machinery-form.component';
import { MachineryService } from '../../core/services/machinery.service';
import { MachineryResponse, MachineryRentalResponse, PagedMachineryResponse } from '../../core/models/machinery.models';
import { MatSnackBar } from '@angular/material/snack-bar';

describe('Phase 8.4 — Machinery Rental Marketplace Component Suite', () => {
  let machineryServiceMock: any;
  let snackBarMock: any;

  const mockMachinery1: MachineryResponse = {
    id: 'mach-1',
    ownerUserId: 'owner-1',
    ownerName: 'Farmer Joe',
    name: 'Mahindra 575 DI',
    category: 'Tractor',
    brand: 'Mahindra',
    model: '575 DI',
    manufacturingYear: 2021,
    description: 'Heavy duty tractor',
    dailyRent: 1500,
    securityDeposit: 500,
    isDriverIncluded: false,
    isFuelIncluded: false,
    driverAvailable: true,
    driverChargePerDay: 400,
    driverName: 'Driver Ramesh',
    driverPhone: '9876543210',
    availabilityStatus: 'Available',
    location: 'Rajkot Farm',
    city: 'Rajkot',
    state: 'Gujarat',
    pincode: '360005',
    isActive: true,
    isFavorited: false,
    isOwnedByCurrentUser: false,
    images: [],
    createdAtUtc: new Date().toISOString(),
    updatedAtUtc: new Date().toISOString()
  };

  const mockMachineryOwned: MachineryResponse = {
    ...mockMachinery1,
    id: 'mach-owned',
    name: 'My Owned Tractor',
    isOwnedByCurrentUser: true
  };

  const mockMachineryNoDriver: MachineryResponse = {
    ...mockMachinery1,
    id: 'mach-nodriver',
    driverAvailable: false,
    driverChargePerDay: 0
  };

  const mockPagedResponse: PagedMachineryResponse = {
    items: [mockMachinery1, mockMachineryOwned],
    totalCount: 2,
    page: 1,
    pageSize: 12,
    totalPages: 1
  };

  const mockRental1: MachineryRentalResponse = {
    id: 'rent-1',
    machineryId: 'mach-1',
    machineryName: 'Mahindra 575 DI',
    machineryCategory: 'Tractor',
    ownerUserId: 'owner-1',
    ownerName: 'Farmer Joe',
    renterUserId: 'renter-1',
    renterName: 'Renter Customer',
    startDate: '2026-09-01',
    endDate: '2026-09-05',
    rentalDays: 5,
    rentPerDaySnapshot: 1500,
    driverChargePerDaySnapshot: 400,
    driverRequired: true,
    machineryAmount: 7500,
    driverAmount: 2000,
    totalAmount: 9500,
    securityDepositSnapshot: 500,
    totalRentAmount: 9500,
    totalPayableAmount: 10000,
    paymentStatus: 'Paid',
    rentalStatus: 'Booked',
    createdAtUtc: new Date().toISOString(),
    updatedAtUtc: new Date().toISOString()
  };

  beforeEach(() => {
    machineryServiceMock = {
      getMachinery: vi.fn().mockReturnValue(of(mockPagedResponse)),
      getMachineryById: vi.fn().mockReturnValue(of(mockMachinery1)),
      getAvailability: vi.fn().mockReturnValue(of({ machineryId: 'mach-1', bookedRanges: [] })),
      bookRental: vi.fn().mockReturnValue(of(mockRental1)),
      getMyRentals: vi.fn().mockReturnValue(of([mockRental1])),
      updateRentalStatus: vi.fn().mockReturnValue(of({})),
      createMachinery: vi.fn().mockReturnValue(of(mockMachinery1)),
      updateMachinery: vi.fn().mockReturnValue(of(mockMachinery1))
    };

    snackBarMock = {
      open: vi.fn()
    };
  });

  describe('CustomerMachineryComponent (Browse Marketplace)', () => {
    let component: CustomerMachineryComponent;
    let fixture: ComponentFixture<CustomerMachineryComponent>;

    beforeEach(async () => {
      await TestBed.configureTestingModule({
        imports: [CustomerMachineryComponent],
        providers: [
          provideRouter([]),
          { provide: MachineryService, useValue: machineryServiceMock }
        ]
      }).compileComponents();

      fixture = TestBed.createComponent(CustomerMachineryComponent);
      component = fixture.componentInstance;
    });

    it('Scenario 1: loads initial machinery list on init', () => {
      fixture.detectChanges();
      expect(machineryServiceMock.getMachinery).toHaveBeenCalled();
      expect(component.result()?.items.length).toBe(2);
    });

    it('Scenario 2: filters by search keyword correctly', () => {
      fixture.detectChanges();
      component.search.set('Mahindra');
      component.applyFilters();
      expect(machineryServiceMock.getMachinery).toHaveBeenCalledWith(expect.objectContaining({ search: 'Mahindra' }));
    });

    it('Scenario 3: filters by category correctly', () => {
      fixture.detectChanges();
      component.selectedCategory.set('Tractor');
      component.applyFilters();
      expect(machineryServiceMock.getMachinery).toHaveBeenCalledWith(expect.objectContaining({ category: 'Tractor' }));
    });

    it('Scenario 4: filters by brand correctly', () => {
      fixture.detectChanges();
      component.brandSearch.set('Kubota');
      component.applyFilters();
      expect(machineryServiceMock.getMachinery).toHaveBeenCalledWith(expect.objectContaining({ brand: 'Kubota' }));
    });

    it('Scenario 5: filters by driver availability option correctly', () => {
      fixture.detectChanges();
      component.driverAvailableFilter.set('true');
      component.applyFilters();
      expect(machineryServiceMock.getMachinery).toHaveBeenCalledWith(expect.objectContaining({ driverAvailable: true }));

      component.driverAvailableFilter.set('false');
      component.applyFilters();
      expect(machineryServiceMock.getMachinery).toHaveBeenCalledWith(expect.objectContaining({ driverAvailable: false }));
    });

    it('Scenario 6: filters by date availability range', () => {
      fixture.detectChanges();
      component.startDate.set('2026-09-01');
      component.endDate.set('2026-09-05');
      component.applyFilters();
      expect(machineryServiceMock.getMachinery).toHaveBeenCalledWith(expect.objectContaining({ startDate: '2026-09-01', endDate: '2026-09-05' }));
    });

    it('Scenario 7: sorts items correctly', () => {
      fixture.detectChanges();
      component.sortBy.set('priceAsc');
      component.applyFilters();
      expect(machineryServiceMock.getMachinery).toHaveBeenCalledWith(expect.objectContaining({ sortBy: 'priceAsc' }));
    });

    it('Scenario 8: resets all filters when resetFilters() is clicked', () => {
      fixture.detectChanges();
      component.search.set('Tractor');
      component.selectedCategory.set('Tractor');
      component.driverAvailableFilter.set('true');
      component.resetFilters();

      expect(component.search()).toBe('');
      expect(component.selectedCategory()).toBe('');
      expect(component.driverAvailableFilter()).toBe('all');
      expect(component.currentPage()).toBe(1);
    });

    it('Scenario 9: provides correct edit route for owned machinery', () => {
      expect(component.getEditRoute('mach-owned')).toBe('/customer/my-machinery/mach-owned/edit');
    });

    it('Scenario 10: provides correct detail route for non-owned machinery', () => {
      expect(component.getDetailRoute('mach-1')).toBe('/customer/machinery/mach-1');
    });

    it('Scenario 11: checks dynamic route properties for farmer url vs customer url', () => {
      expect(component.newMachineryRoute).toBe('/customer/my-machinery/new');
    });
  });

  describe('CustomerMachineryDetailComponent (Booking Details)', () => {
    let component: CustomerMachineryDetailComponent;
    let fixture: ComponentFixture<CustomerMachineryDetailComponent>;

    beforeEach(async () => {
      await TestBed.configureTestingModule({
        imports: [CustomerMachineryDetailComponent],
        providers: [
          provideRouter([]),
          { provide: MachineryService, useValue: machineryServiceMock },
          { provide: MatSnackBar, useValue: snackBarMock }
        ]
      }).compileComponents();

      fixture = TestBed.createComponent(CustomerMachineryDetailComponent);
      component = fixture.componentInstance;
    });

    it('Scenario 12: loads machinery detail and availability', () => {
      component.loadDetail('mach-1');
      expect(machineryServiceMock.getMachineryById).toHaveBeenCalledWith('mach-1');
      expect(machineryServiceMock.getAvailability).toHaveBeenCalledWith('mach-1');
      expect(component.machinery()?.name).toBe('Mahindra 575 DI');
    });

    it('Scenario 13: defaults driver choice to false when driverAvailable is true', () => {
      component.loadDetail('mach-1');
      expect(component.driverRequired()).toBe(false);
    });

    it('Scenario 14: calculates machinery amount, driver amount, total amount, and total payable correctly', () => {
      component.machinery.set(mockMachinery1);
      component.startDate.set('2026-09-01');
      component.endDate.set('2026-09-05'); // 5 days
      component.driverRequired.set(true); // driver required
      component.recalculateFinancials();

      expect(component.calculatedDays()).toBe(5);
      expect(component.calculatedMachineryAmount()).toBe(7500); // 5 * 1500
      expect(component.calculatedDriverAmount()).toBe(2000); // 5 * 400
      expect(component.calculatedTotalAmount()).toBe(9500); // 7500 + 2000
      expect(component.calculatedTotalPayable()).toBe(10000); // 9500 + 500
    });

    it('Scenario 15: disables driver choice option when driverAvailable is false', () => {
      component.machinery.set(mockMachineryNoDriver);
      component.onDriverOptionChanged(true);
      expect(component.driverRequired()).toBe(false);
    });

    it('Scenario 16: recalculates financials when driver selection is toggled', () => {
      component.machinery.set(mockMachinery1);
      component.startDate.set('2026-09-01');
      component.endDate.set('2026-09-05');

      component.onDriverOptionChanged(false);
      expect(component.calculatedDriverAmount()).toBe(0);
      expect(component.calculatedTotalPayable()).toBe(8000); // 7500 + 0 + 500

      component.onDriverOptionChanged(true);
      expect(component.calculatedDriverAmount()).toBe(2000);
      expect(component.calculatedTotalPayable()).toBe(10000);
    });

    it('Scenario 17: disables booking form and sets error when machinery is owned by current user', () => {
      component.machinery.set(mockMachineryOwned);
      component.recalculateFinancials();
      expect(component.bookingError()).toBe('You own this machinery listing and cannot rent it.');
    });

    it('Scenario 18: rejects invalid date selection (end date before start date)', () => {
      component.machinery.set(mockMachinery1);
      component.startDate.set('2026-09-05');
      component.endDate.set('2026-09-01');
      component.recalculateFinancials();
      expect(component.bookingError()).toBe('End date must be on or after start date.');
    });

    it('Scenario 19: rejects overlapping date selection against booked ranges', () => {
      component.machinery.set(mockMachinery1);
      component.availability.set({
        machineryId: 'mach-1',
        bookedRanges: [{ startDate: '2026-09-01', endDate: '2026-09-05' }]
      });
      component.startDate.set('2026-09-03');
      component.endDate.set('2026-09-07');
      component.recalculateFinancials();
      expect(component.bookingError()).toBe('The selected date range overlaps with an existing booking.');
    });

    it('Scenario 20: submits bookRental with driverRequired payload', () => {
      component.machinery.set(mockMachinery1);
      component.startDate.set('2026-09-01');
      component.endDate.set('2026-09-05');
      component.driverRequired.set(true);
      component.recalculateFinancials();

      component.confirmBooking();
      expect(machineryServiceMock.bookRental).toHaveBeenCalledWith('mach-1', expect.objectContaining({
        startDate: '2026-09-01',
        endDate: '2026-09-05',
        driverRequired: true
      }));
    });
  });

  describe('CustomerMyRentalsComponent (Renter Bookings)', () => {
    let component: CustomerMyRentalsComponent;
    let fixture: ComponentFixture<CustomerMyRentalsComponent>;

    beforeEach(async () => {
      await TestBed.configureTestingModule({
        imports: [CustomerMyRentalsComponent],
        providers: [
          provideRouter([]),
          { provide: MachineryService, useValue: machineryServiceMock },
          { provide: MatSnackBar, useValue: snackBarMock }
        ]
      }).compileComponents();

      fixture = TestBed.createComponent(CustomerMyRentalsComponent);
      component = fixture.componentInstance;
    });

    it('Scenario 21: loads user rental bookings and filters by tab', () => {
      fixture.detectChanges();
      expect(machineryServiceMock.getMyRentals).toHaveBeenCalled();
      expect(component.filterRentals().length).toBe(1);

      component.onTabChange('Completed');
      expect(component.filterRentals().length).toBe(0);
    });

    it('Scenario 22: renders status badge classes correctly', () => {
      expect(component.getStatusBadgeClass('Booked')).toContain('bg-amber-500/20');
      expect(component.getStatusBadgeClass('Completed')).toContain('bg-emerald-500/20');
    });

    it('Scenario 23: allows renter to return machinery when status is RentedOut', () => {
      component.returnRental('rent-1');
      expect(machineryServiceMock.updateRentalStatus).toHaveBeenCalledWith('rent-1', { newStatus: 'Returned' });
    });

    it('Scenario 24: allows renter to cancel booking when status is Booked or Confirmed', () => {
      vi.spyOn(window, 'prompt').mockReturnValue('Plans changed');
      component.cancelRental('rent-1');
      expect(machineryServiceMock.updateRentalStatus).toHaveBeenCalledWith('rent-1', {
        newStatus: 'Cancelled',
        cancellationReason: 'Plans changed'
      });
    });
  });

  describe('MyMachineryFormComponent (Form & Driver Configuration)', () => {
    let component: MyMachineryFormComponent;
    let fixture: ComponentFixture<MyMachineryFormComponent>;

    beforeEach(async () => {
      await TestBed.configureTestingModule({
        imports: [MyMachineryFormComponent],
        providers: [
          provideRouter([]),
          { provide: MachineryService, useValue: machineryServiceMock },
          { provide: MatSnackBar, useValue: snackBarMock }
        ]
      }).compileComponents();

      fixture = TestBed.createComponent(MyMachineryFormComponent);
      component = fixture.componentInstance;
    });

    it('Scenario 25: binds driverAvailable, driverChargePerDay, driverName, driverPhone, driverNotes and submits payload correctly', () => {
      component.name.set('New Combine Harvester');
      component.category.set('Harvester');
      component.dailyRent.set(5000);
      component.location.set('Surat Farm');
      component.driverAvailable.set(true);
      component.driverChargePerDay.set(800);
      component.driverName.set('Suresh Operator');
      component.driverPhone.set('9876543211');
      component.driverNotes.set('Expert harvester driver');

      component.saveMachinery();

      expect(machineryServiceMock.createMachinery).toHaveBeenCalledWith(expect.objectContaining({
        name: 'New Combine Harvester',
        category: 'Harvester',
        dailyRent: 5000,
        driverAvailable: true,
        driverChargePerDay: 800,
        driverName: 'Suresh Operator',
        driverPhone: '9876543211',
        driverNotes: 'Expert harvester driver'
      }));
    });
  });
});
