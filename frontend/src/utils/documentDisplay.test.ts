import { describe, it, expect } from 'vitest';
import { makeDocument } from '../test/factories';
import {
  applicantName,
  displayFileNumber,
  executedFullName,
  fileNumberLabel,
  fullName,
  publicEntityBranch,
  tripleName,
} from './documentDisplay';

describe('documentDisplay', () => {
  describe('fullName', () => {
    it('يعيد الاسم الثلاثي للمقترض في الملفات غير «منفذ عليه»', () => {
      expect(fullName(makeDocument())).toBe('أحمد خالد الخطيب');
    });

    it('يعيد أول شخص طبيعي منفذ عليه في وضع «منفذ عليه»', () => {
      const d = makeDocument({
        generalEntitySide: 'executed',
        executedNaturalPersons: [{ id: 1, name: 'سامر', father: 'حسن', family: 'علي' }],
      });
      expect(fullName(d)).toBe('سامر حسن علي');
    });

    it('يعيد الجهة العامة عند غياب الشخص الطبيعي في وضع «منفذ عليه»', () => {
      const d = makeDocument({
        generalEntitySide: 'executed',
        executedPublicEntities: [{ id: 1, entityName: 'المصرف العقاري', entityBranch: 'فرع المزة' }],
      });
      expect(fullName(d)).toBe('المصرف العقاري');
    });

    it('يعيد طالب التنفيذ كبديل عند غياب المنفذ عليهم جميعًا', () => {
      const d = makeDocument({ generalEntitySide: 'executed' });
      expect(fullName(d)).toBe('المدعي');
    });

    it('يعامل صفة «عرض وايداع» مثل «منفذ عليه» ويعيد أول شخص طبيعي معروض', () => {
      const d = makeDocument({
        generalEntitySide: 'deposit',
        executedNaturalPersons: [{ id: 1, name: 'سامر', father: 'حسن', family: 'علي' }],
      });
      expect(fullName(d)).toBe('سامر حسن علي');
    });
  });

  describe('executedFullName', () => {
    it('يفضل الشخص الطبيعي ثم الجهة ثم طالب التنفيذ', () => {
      const d = makeDocument({
        generalEntitySide: 'executed',
        executedPublicEntities: [{ id: 1, entityName: 'المصرف العقاري', entityBranch: '' }],
      });
      expect(executedFullName(d)).toBe('المصرف العقاري');
      expect(executedFullName(makeDocument())).toBe('المدعي');
    });
  });

  describe('applicantName', () => {
    it('يعيد أول طالب تنفيذ بالاسم الثلاثي في وضع «منفذ عليه»', () => {
      const d = makeDocument({
        generalEntitySide: 'executed',
        executionApplicants: [{ id: 1, name: 'أحمد', father: 'خالد', family: 'الخطيب' }],
      });
      expect(applicantName(d)).toBe('أحمد خالد الخطيب');
    });

    it('يعيد الحقل المباشر عند غياب طلبات التنفيذ', () => {
      expect(applicantName(makeDocument())).toBe('المدعي');
    });

    it('يعيد أول طالب عرض بالاسم الثلاثي في صفة «عرض وايداع»', () => {
      const d = makeDocument({
        generalEntitySide: 'deposit',
        executionApplicants: [{ id: 1, name: 'هاني', father: 'سامر', family: 'النجار' }],
      });
      expect(applicantName(d)).toBe('هاني سامر النجار');
    });
  });

  describe('publicEntityBranch', () => {
    it('يعيد فرع أول جهة عامة طالبة للتنفيذ في الوضع العادي', () => {
      const d = makeDocument({
        applicantPublicEntities: [{ id: 1, name: 'المصرف التجاري السوري', branch: 'فرع 1', governorate: 'دمشق' }],
      });
      expect(publicEntityBranch(d)).toBe('فرع 1');
    });

    it('يعيد فرع أول جهة عامة منفذ عليها (public) في وضع «منفذ عليه»', () => {
      const d = makeDocument({
        generalEntitySide: 'executed',
        executedPublicEntities: [
          { id: 1, entityName: 'المصرف العقاري', entityBranch: 'فرع المزة', nature: 'public' },
        ],
      });
      expect(publicEntityBranch(d)).toBe('فرع المزة');
    });

    it('يتجاهل الشخص الاعتباري (legal) في وضع «منفذ عليه»', () => {
      const d = makeDocument({
        generalEntitySide: 'executed',
        executedPublicEntities: [{ id: 1, entityName: 'شركة الهدى', entityBranch: 'فرع الشام', nature: 'legal' }],
      });
      expect(publicEntityBranch(d)).toBe('');
    });

    it('يعيد سلسلة فارغة عند غياب أي جهة عامة', () => {
      expect(publicEntityBranch(makeDocument())).toBe('');
    });
  });

  describe('displayFileNumber', () => {
    it('يعيد رقمًا فارغًا للمسودة', () => {
      expect(displayFileNumber(makeDocument({ isDraft: true, fileNumber: '99' }))).toBe('');
    });

    it('يضمّن النوع إلى الرقم عند وجوده', () => {
      expect(displayFileNumber(makeDocument())).toBe('99 حقوق');
    });

    it('يعيد الرقم وحده عند غياب النوع', () => {
      expect(displayFileNumber(makeDocument({ fileType: '' }))).toBe('99');
    });
  });

  describe('tripleName', () => {
    it('يجمع المكونات الثلاثة بفاصل بينها', () => {
      expect(tripleName('أحمد', 'خالد', 'الخطيب')).toBe('أحمد خالد الخطيب');
    });

    it('يتجاهل أي مكون فارغ', () => {
      expect(tripleName('أحمد', undefined, 'الخطيب')).toBe('أحمد الخطيب');
      expect(tripleName('أحمد', '', '')).toBe('أحمد');
    });

    it('يعيد سلسلة فارغة عند غياب كل المكونات', () => {
      expect(tripleName()).toBe('');
      expect(tripleName('', '', '')).toBe('');
    });
  });

  describe('fileNumberLabel', () => {
    it('يضمّن النوع إلى الرقم عند وجوده', () => {
      expect(fileNumberLabel('99', 'حقوق')).toBe('99 حقوق');
    });

    it('يعيد الرقم وحده عند غياب النوع', () => {
      expect(fileNumberLabel('99', '')).toBe('99');
      expect(fileNumberLabel('99', undefined)).toBe('99');
    });

    it('يعيد النوع وحده عند غياب الرقم ويعيد سلسلة فارغة عند غيابهما', () => {
      expect(fileNumberLabel(undefined, 'حقوق')).toBe('حقوق');
      expect(fileNumberLabel(undefined, undefined)).toBe('');
    });
  });
});
